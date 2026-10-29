using GalaxyIO.DOTS.Client.Systems;
using GalaxyIO.DOTS.Components.Actor;
using GalaxyIO.DOTS.Components.Message;
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
    /// <see cref="PlayerInputComponent"/>를 읽어들여 터렛의 투사체 스폰 메시지를 남기는 시스템.
    /// 총구 사양은 <see cref="EquippedTurretComponent"/>의 포탑에서 찾음.
    /// 쿨다운은 롤백 재시뮬레이션까지 매 틱 진행해 서버와 같은 틱에 쏨.
    /// 포탑 열(<see cref="TurretHeatComponent"/>)도 같이 계산함. 과열 중에는 방아쇠를 뗀 것으로 다룸.
    /// 발사 간격은 재장전 강화만큼 줄고, 포탑에 반동이 있으면 우주선 속도를 발사 반대 방향으로 밂.
    /// 충전 포탑은 쿨다운이 끝났을 때만 충전하고, 다 찬 뒤 떼면 쏨(<see cref="TurretSwitch.UpdateCharge"/>).
    /// 발사대(<see cref="TurretRackComponent"/>)가 있으면 장전 상태도 갱신함.
    /// 클라이언트와 서버가 같은 틱에 각자 투사체를 만들고
    /// Ghost 투사체는 <see cref="ProjectileSpawnClassificationSystem"/>이 둘을 짝지음.
    /// 주인 클라이언트의 Shot 투사체는 서버와 맞추지 않고 롤백되지 않는 쿨다운(<see cref="ShotCooldownElement"/>)으로 쏨.
    /// </summary>
    [UpdateInGroup(typeof(UpdateSystemGroup))]
    [UpdateAfter(typeof(PlayerMovementSystem))]
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

            float deltaTime = SystemAPI.Time.DeltaTime;
            ItemConfigComponent config = SystemAPI.GetSingleton<ItemConfigComponent>();
            uint spawnTick = networkTime.ServerTick.SerializedData;

            DynamicBuffer<ProjectileSpawnMessage> spawns = SystemAPI.GetSingletonBuffer<ProjectileSpawnMessage>();

            // 우주선마다 비워서 다시 씀.
            var shots = new NativeList<TurretShotRequest>(8, Allocator.Temp);
            var ownShots = new NativeList<TurretShotRequest>(8, Allocator.Temp);

            foreach (
                var (transform, inputRequest, movement, owner, delay, equipped, cooldowns, spaceship) in
                    SystemAPI.Query<
                        RefRO<LocalTransform>,
                        RefRO<PlayerInputComponent>,
                        RefRW<MovementComponent>,
                        RefRO<GhostOwner>,
                        RefRO<CommandDataInterpolationDelay>,
                        RefRO<EquippedTurretComponent>,
                        DynamicBuffer<TurretCooldownElement>
                    >().WithAll<Simulate>().WithEntityAccess()
            ) {
                // 쿼리 타입 인자 수 제한(7개)으로 따로 가져옴.
                RefRW<TurretSwitchComponent> turretSwitch = SystemAPI.GetComponentRW<TurretSwitchComponent>(spaceship);
                RefRW<TurretStandbyComponent> standby = SystemAPI.GetComponentRW<TurretStandbyComponent>(spaceship);
                RefRW<TurretHeatComponent> heat = SystemAPI.GetComponentRW<TurretHeatComponent>(spaceship);
                ItemRegistryElement turret = registry.Get(equipped.ValueRO.Value);
                TurretSpecData spec = turret.Turret;

                // 사격 시작/중지 전환. 전환 중에는 쏠 수 없음. 충전 포탑은 다 찬 뒤 떼면 쏨.
                // 과열 중이거나 조종 포탑 총구가 없으면 방아쇠를 뗀 것으로 넘김.
                // 복제된 전환 상태로 다른 클라이언트의 연출, 반복음도 멈춤.
                bool trigger = inputRequest.ValueRO.TurretTriggered && !heat.ValueRO.Overheated && cooldowns.Length > 0;

                bool firing = turret.ReleaseFire
                    ? TurretSwitch.UpdateCharge(
                        ref turretSwitch.ValueRW,
                        trigger,
                        cooldowns.Length > 0 && cooldowns[0].Value <= 0.0F,
                        turret.SwitchTime,
                        deltaTime
                    )
                    : TurretSwitch.Update(ref turretSwitch.ValueRW, trigger, turret.SwitchTime, deltaTime);

                // 강화한 피해 배율, 발사 간격 감소량. 강화가 없는 기체는 1, 0.
                float damageScale = 1.0F;
                float reloadBonus = 0.0F;

                if (SystemAPI.HasBuffer<UpgradeParamElement>(spaceship))
                {
                    DynamicBuffer<UpgradeParamElement> parameters = SystemAPI.GetBuffer<UpgradeParamElement>(spaceship);
                    DynamicBuffer<UpgradeLevelElement> levels = SystemAPI.GetBuffer<UpgradeLevelElement>(spaceship);
                    damageScale = Upgrade.Value(parameters, levels, UpgradeParam.Damage);
                    reloadBonus = Upgrade.Value(parameters, levels, UpgradeParam.ReloadTime);
                }

                // 다음 발사부터 줄어든 간격으로 기다림. 쏠 수 없는 포탑(간격 0)은 그대로 둠.
                // 0 이하로 줄면 쿨다운 없이 매 틱 쏨. 원래 한 틱보다 짧은 포탑은 원래 간격까지만 줄어듦.
                if (spec.MaxFireCooldown > 0.0F && reloadBonus > 0.0F)
                {
                    spec.MaxFireCooldown = math.max(
                        spec.MaxFireCooldown - reloadBonus,
                        math.min(spec.MaxFireCooldown, deltaTime)
                    );
                }

                NativeArray<float> cooldownValues = cooldowns.Reinterpret<float>().AsNativeArray();

                // 충전 포탑을 다 차기 전에 떼면 쏜 것처럼 쿨다운이 돎.
                if (turret.ReleaseFire && turretSwitch.ValueRO.State == TurretSwitchState.SpinningDown)
                {
                    cooldownValues[0] = spec.MaxFireCooldown;
                }

                shots.Clear();
                TurretCycle.Advance(
                    cooldownValues,
                    spec,
                    firing,
                    deltaTime,
                    ref shots
                );

                // 발사대가 있는 기종은 장전 상태를 복제해 다른 클라이언트도 그리게 함.
                if (SystemAPI.HasComponent<TurretRackComponent>(spaceship))
                {
                    RefRW<TurretRackComponent> rack = SystemAPI.GetComponentRW<TurretRackComponent>(spaceship);
                    rack.ValueRW.Loaded = TurretRack.Loaded(cooldownValues, rack.ValueRO.ShowTime);
                }

                // 총구 위치. 쿨다운과 같은 순서.
                DynamicBuffer<TurretElement> muzzles = SystemAPI.GetBuffer<TurretElement>(spaceship);

                // 반동. 속도는 예측 대상이라 스폰 여부와 관계없이 롤백 재시뮬레이션에서도 같게 줌.
                if (turret.Recoil > 0.0F)
                {
                    foreach (TurretShotRequest request in shots)
                    {
                        LocalTransform muzzle = muzzles[request.Muzzle].MuzzleLocation;
                        movement.ValueRW.Velocity -= transform.ValueRO.TransformTransform(muzzle).Up().xy * turret.Recoil;
                    }
                }

                // 주인 클라이언트가 직접 쏘는 Shot 투사체는 롤백되지 않는 쿨다운으로 쏨. 처음 예측하는 틱에만 진행함.
                // 롤백된 쿨다운으로 쏘면 서버와 발사 틱이 어긋날 때 같은 발을 두 번 쏘거나 빠뜨림.
                // 다른 포탑을 쓰는 동안에도 진행해 포탑을 바꾼 직후의 첫 발이 밀리지 않음.
                ownShots.Clear();

                if (!isServer && spawn)
                {
                    DynamicBuffer<ShotCooldownElement> own = SystemAPI.GetBuffer<ShotCooldownElement>(spaceship);

                    if (own.Length != cooldowns.Length)
                    {
                        own.Resize(cooldowns.Length, NativeArrayOptions.ClearMemory);
                    }

                    NativeArray<float> ownValues = own.Reinterpret<float>().AsNativeArray();

                    if (turret.ReleaseFire && turretSwitch.ValueRO.State == TurretSwitchState.SpinningDown)
                    {
                        ownValues[0] = spec.MaxFireCooldown;
                    }

                    TurretCycle.Advance(ownValues, spec, firing, deltaTime, ref ownShots);
                }

                NativeList<TurretShotRequest> spawnShots =
                    !isServer && spec.Replication == ProjectileReplication.Shot ? ownShots : shots;

                foreach (TurretShotRequest request in spawnShots)
                {
                    int i = request.Muzzle;

                    LocalTransform muzzle = muzzles[i].MuzzleLocation;

                    // 각자 발사하는 투사체(Local)는 서버만 스폰함. 클라이언트 연출은 LocalProjectileSystem 이 냄.
                    // 발사 정보를 보내는 투사체(Shot)는 주인 클라이언트도 직접 쏨. 다른 클라이언트는 서버가 보낸 정보로 쏨.
                    // Ghost Prefab이 등록되기 전에는 예측 스폰한 투사체를 초기화할 수 없음.
                    // 스폰 여부와 관계없이 쿨다운은 서버와 같게 진행함.
                    bool canSpawn = spec.Replication switch
                    {
                        ProjectileReplication.Local => isServer,
                        ProjectileReplication.Shot  => spawn,
                        _ => spawn && registeredGhosts.ContainsKey(SystemAPI.GetComponent<GhostType>(spec.ProjectilePrefab)),
                    };

                    if (!canSpawn)
                    {
                        continue;
                    }

                    float speed = SystemAPI.HasComponent<StraightforwardProjectileComponent>(spec.ProjectilePrefab)
                        ? SystemAPI.GetComponent<StraightforwardProjectileComponent>(spec.ProjectilePrefab).Speed
                        : 0.0F;

                    for (uint j = 0; j < spec.ProjectileCount; j++)
                    {
                        // 클라이언트와 서버가 같은 값을 내는 식별자.
                        // 탄퍼짐 시드와 스폰 분류 키를 겸함.
                        uint spawnId = math.hash(new uint4((uint)owner.ValueRO.NetworkId, spawnTick, (uint)i, request.Shot * 64 + j));

                        spawns.Add(new ProjectileSpawnMessage
                        {
                            Prefab             = spec.ProjectilePrefab,
                            Spawner            = spaceship,
                            Muzzle             = TurretShot.Aim(muzzle, spec, spawnId, out float speedScale),

                            // 투사체가 쏜 우주선과 같은 타임라인에 머물게 함. (OwnerPredicted)
                            NetworkId          = owner.ValueRO.NetworkId,
                            SpawnId            = spawnId,
                            Turret             = equipped.ValueRO.Value,
                            Damage             = spec.Damage * damageScale,
                            InterpolationDelay = delay.ValueRO.Delay,
                            Speed              = speed * speedScale,

                            // 기수 방향으로 나아가는 성분만 물려줌.
                            // 호버링으로 옆으로 흐르는 성분까지 더하면 총구 방향과 어긋난 가속이 붙음.
                            InheritAxis        = new float2(0.0F, 1.0F),
                        });
                    }
                }

                // 채널링 무기는 방아쇠를 당긴 순간(시작 전환)부터 쏜 것으로 침.
                bool channeling = turret.HeatMode == TurretHeatMode.PerSecond
                                  && (turretSwitch.ValueRO.State == TurretSwitchState.SpinningUp
                                      || turretSwitch.ValueRO.State == TurretSwitchState.Firing);

                TurretStandby.Update(ref standby.ValueRW, channeling || !shots.IsEmpty, deltaTime, config.NoFireStandbyTime);

                // 한 번 사격에 열 한 번. 첫 총구만 세고, 한 틱에 여러 번 쏘면 그만큼 셈.
                uint firings = 0;

                foreach (TurretShotRequest request in shots)
                {
                    if (request.Muzzle == 0)
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
