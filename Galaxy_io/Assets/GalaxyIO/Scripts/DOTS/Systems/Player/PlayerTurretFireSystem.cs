using GalaxyIO.DOTS.Client.Systems;
using GalaxyIO.DOTS.Components.Actor;
using GalaxyIO.DOTS.Components.Singleton;
using GalaxyIO.DOTS.Spaceship;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.NetCode;
using Unity.Transforms;

namespace GalaxyIO.DOTS.Systems
{
    /// <summary>
    /// <see cref="PlayerInputComponent"/>를 읽어들여 터렛의 투사체를 생성하는 시스템.
    /// 총구 사양은 <see cref="EquippedTurretComponent"/>의 포탑에서 찾음.
    /// 쿨다운은 롤백 재시뮬레이션까지 매 틱 진행해 서버와 같은 틱에 쏨.
    /// 포탑 열(<see cref="TurretHeatComponent"/>)도 같이 계산함. 과열 중에는 방아쇠를 뗀 것으로 다룸.
    /// 클라이언트와 서버가 같은 틱에 각자 투사체를 만들고
    /// <see cref="ProjectileSpawnClassificationSystem"/>이 둘을 짝지음.
    /// </summary>
    [UpdateInGroup(typeof(SpawnSystemGroup))]
    public partial struct PlayerTurretFireSystem : ISystem
    {
        private bool isServer;

        public void OnCreate(ref SystemState state)
        {
            isServer = state.WorldUnmanaged.IsServer();

            state.RequireForUpdate<PlayerInputComponent>();
            state.RequireForUpdate<ItemRegistryElement>();
            state.RequireForUpdate<ItemConfigComponent>();
            state.RequireForUpdate<NetworkTime>();

            // Ghost Prefab 등록 전이거나 InGame 이전에 예측 스폰하면 초기화에 실패하거나
            // 만들자마자 폐기됨.
            state.RequireForUpdate<GhostCollection>();
            state.RequireForUpdate<NetworkStreamInGame>();
            state.RequireForUpdate<EndPredictedSimulationEntityCommandBufferSystem.Singleton>();
        }

        // 플레이어 입력 > 터렛 투사체 생성
        public void OnUpdate(ref SystemState state)
        {
            NetworkTime networkTime = SystemAPI.GetSingleton<NetworkTime>();

            // 롤백 재시뮬레이션에서는 쿨다운만 진행하고 같은 발사를 다시 스폰하지 않음.
            // 서버에서는 이 플래그가 항상 켜져 있음.
            bool spawn = networkTime.IsFirstTimeFullyPredictingTick;

            NativeHashMap<GhostType, int>.ReadOnly registeredGhosts =
                SystemAPI.GetSingleton<GhostCollection>().GhostTypeToColletionIndex;

            Entity registryEntity = SystemAPI.GetSingletonEntity<ItemRegistryElement>();
            DynamicBuffer<ItemRegistryElement> registry = SystemAPI.GetBuffer<ItemRegistryElement>(registryEntity);
            DynamicBuffer<TurretSpecElement> specs = SystemAPI.GetBuffer<TurretSpecElement>(registryEntity);

            float deltaTime = SystemAPI.Time.DeltaTime;
            ItemConfigComponent config = SystemAPI.GetSingleton<ItemConfigComponent>();
            uint spawnTick = networkTime.ServerTick.SerializedData;

            // 첫 투사체를 만들 때 한 번만 생성. 모든 우주선이 같은 버퍼를 씀.
            var commandBuffer = new EntityCommandBuffer();

            // 우주선마다 비워서 다시 씀.
            var shots = new NativeList<TurretShotRequest>(8, Allocator.Temp);

            foreach (
                var (transform, inputRequest, movement, owner, delay, equipped, cooldowns, spaceship) in
                    SystemAPI.Query<
                        RefRO<LocalTransform>,
                        RefRO<PlayerInputComponent>,
                        RefRO<SpaceshipMovementComponent>,
                        RefRO<GhostOwner>,
                        RefRO<CommandDataInterpolationDelay>,
                        RefRO<EquippedTurretComponent>,
                        DynamicBuffer<TurretCooldownElement>
                    >().WithAll<Simulate>().WithEntityAccess()
            ) {
                // 기수 방향으로 나아가는 성분만 투사체에 물려줌.
                // 호버링으로 옆으로 흐르는 성분까지 더하면 총구 방향과 어긋난 가속이 붙음.
                float forwardSpeed = math.dot(
                    movement.ValueRO.DeltaMovement,
                    math.normalizesafe(SpaceshipHeading.FromRotation(transform.ValueRO.Rotation), float2.zero)
                );

                // 쿼리 타입 인자 수 제한(7개)으로 따로 가져옴.
                RefRW<TurretSwitchComponent> turretSwitch = SystemAPI.GetComponentRW<TurretSwitchComponent>(spaceship);
                RefRW<TurretStandbyComponent> standby = SystemAPI.GetComponentRW<TurretStandbyComponent>(spaceship);
                RefRW<TurretHeatComponent> heat = SystemAPI.GetComponentRW<TurretHeatComponent>(spaceship);
                ItemRegistryElement turret = registry.Get(equipped.ValueRO.Value);

                // 사격 시작/중지 전환. 전환 중에는 쏠 수 없음.
                // 과열 중에는 방아쇠를 뗀 것으로 넘김. 복제된 전환 상태로 다른 클라이언트의 연출, 반복음도 멈춤.
                bool firing = TurretSwitch.Update(
                    ref turretSwitch.ValueRW,
                    inputRequest.ValueRO.TurretTriggered && !heat.ValueRO.Overheated,
                    turret.SwitchTime,
                    deltaTime
                );

                shots.Clear();
                TurretCycle.Advance(
                    cooldowns.Reinterpret<float>().AsNativeArray(),
                    registry,
                    specs,
                    equipped.ValueRO.Value,
                    firing,
                    deltaTime,
                    ref shots
                );

                // 강화한 피해 배율. 강화가 없는 기체는 1.
                float damageScale = SystemAPI.HasBuffer<UpgradeParamElement>(spaceship)
                    ? Upgrade.Value(
                        SystemAPI.GetBuffer<UpgradeParamElement>(spaceship),
                        SystemAPI.GetBuffer<UpgradeLevelElement>(spaceship),
                        UpgradeParam.Damage)
                    : 1.0F;

                // 총구 위치. 쿨다운과 같은 순서.
                DynamicBuffer<TurretElement> muzzles = SystemAPI.GetBuffer<TurretElement>(spaceship);

                foreach (TurretShotRequest request in shots)
                {
                    int i = request.Muzzle;
                    TurretSpecElement spec = registry.GetTurretSpec(specs, equipped.ValueRO.Value, i);

                    // 각자 발사하는 투사체는 서버만 스폰함. 클라이언트 연출은 LocalProjectileSystem 이 냄.
                    // Ghost Prefab이 등록되기 전에는 예측 스폰한 투사체를 초기화할 수 없음.
                    // 스폰 여부와 관계없이 쿨다운은 서버와 같게 진행함.
                    bool canSpawn = spec.LocalLaunch
                        ? isServer
                        : spawn && registeredGhosts.ContainsKey(SystemAPI.GetComponent<GhostType>(spec.ProjectilePrefab));

                    if (!canSpawn)
                    {
                        continue;
                    }

                    if (!commandBuffer.IsCreated)
                    {
                        commandBuffer = SystemAPI
                            .GetSingleton<EndPredictedSimulationEntityCommandBufferSystem.Singleton>()
                            .CreateCommandBuffer(state.WorldUnmanaged);
                    }

                    // 터렛의 로컬 좌표를 월드 좌표로 합성. 원본 Transform은 수정하지 않음.
                    LocalTransform muzzleTransform = transform.ValueRO.TransformTransform(muzzles[i].MuzzleLocation);

                    // 발사한 우주선과 그 시점의 보간 지연을 기록.
                    // 나머지 값은 프리팹 베이킹 결과를 그대로 씀.
                    var shared = SystemAPI.GetComponent<SharedProjectileComponent>(spec.ProjectilePrefab);
                    shared.Owner = spaceship;
                    shared.TurretIndex = (byte)i;
                    shared.Damage = spec.Damage * damageScale;
                    shared.InterpolationDelay = delay.ValueRO.Delay;

                    bool straight = SystemAPI.HasComponent<StraightforwardProjectileComponent>(spec.ProjectilePrefab);
                    StraightforwardProjectileComponent straightforward = straight
                        ? SystemAPI.GetComponent<StraightforwardProjectileComponent>(spec.ProjectilePrefab)
                        : default;

                    for (uint j = 0; j < spec.ProjectileCount; j++)
                    {
                        // 클라이언트와 서버가 같은 값을 내는 식별자.
                        // 탄퍼짐 시드와 스폰 분류 키를 겸함.
                        uint spawnId = math.hash(new uint4((uint)owner.ValueRO.NetworkId, spawnTick, (uint)i, request.Shot * 64 + j));

                        Entity projectile = commandBuffer.Instantiate(spec.ProjectilePrefab);
                        commandBuffer.SetComponent(
                            projectile,
                            TurretShot.Aim(muzzleTransform, spec, spawnId, out float speedScale)
                        );

                        // 투사체가 쏜 우주선과 같은 타임라인에 머물게 함. (OwnerPredicted)
                        commandBuffer.SetComponent(projectile, new GhostOwner { NetworkId = owner.ValueRO.NetworkId });
                        commandBuffer.SetComponent(projectile, new ProjectileSpawnIdComponent { Value = spawnId });

                        // 쏜 우주선의 진영을 물려받음.
                        commandBuffer.SetComponent(projectile, SystemAPI.GetComponent<FactionComponent>(spaceship));
                        commandBuffer.SetComponent(projectile, shared);

                        // 기수 방향으로 나가는 직진 투사체는 우주선의 속력을 물려받음.
                        if (straight)
                        {
                            StraightforwardProjectileComponent launched = straightforward;
                            launched.Speed = straightforward.Speed * speedScale
                                             + (straightforward.ReflectOwnerVelocity ? forwardSpeed : 0.0F);
                            commandBuffer.SetComponent(projectile, launched);
                        }
                    }
                }

                // 채널링 무기는 방아쇠를 당긴 순간(시작 전환)부터 쏜 것으로 침.
                bool channeling = turret.HeatMode == TurretHeatMode.PerSecond
                                  && (turretSwitch.ValueRO.State == TurretSwitchState.SpinningUp
                                      || turretSwitch.ValueRO.State == TurretSwitchState.Firing);

                TurretStandby.Update(ref standby.ValueRW, channeling || !shots.IsEmpty, deltaTime, config.NoFireStandbyTime);

                // 한 번 사격에 열 한 번. 총구 수와 따라 쏘는 총구는 세지 않고, 한 틱에 여러 번 쏘면 그만큼 셈.
                uint firings = 0;

                foreach (TurretShotRequest request in shots)
                {
                    if (registry.GetTurretSpec(specs, equipped.ValueRO.Value, request.Muzzle).FollowMuzzle < 0)
                    {
                        firings = math.max(firings, request.Shot + 1);
                    }
                }

                float gain = turret.HeatMode == TurretHeatMode.PerSecond
                    ? channeling ? turret.Heat * deltaTime : 0.0F
                    : turret.Heat * firings;

                TurretHeat.Update(
                    ref heat.ValueRW,
                    gain,
                    standby.ValueRO.NoFireTime >= config.NoFireStandbyTime,
                    config.CoolingTime,
                    config.OverheatCoolingTime,
                    deltaTime
                );
            }
        }
    }
}
