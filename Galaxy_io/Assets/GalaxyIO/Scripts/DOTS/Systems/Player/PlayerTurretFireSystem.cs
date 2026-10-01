using GalaxyIO.DOTS.Client.Systems;
using GalaxyIO.DOTS.Components.Actor;
using GalaxyIO.DOTS.Components.Singleton;
using GalaxyIO.DOTS.Spaceship;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.NetCode;
using Unity.Transforms;
using Random = Unity.Mathematics.Random;

namespace GalaxyIO.DOTS.Systems
{
    /// <summary>
    /// <see cref="PlayerInputComponent"/>를 읽어들여 터렛의 투사체를 생성하는 시스템.
    /// 총구 사양은 <see cref="EquippedTurretComponent"/>의 포탑에서 찾음.
    /// 클라이언트와 서버가 같은 틱에 각자 투사체를 만들고
    /// <see cref="ProjectileSpawnClassificationSystem"/>이 둘을 짝지음.
    /// </summary>
    [UpdateInGroup(typeof(SpawnSystemGroup))]
    public partial struct PlayerTurretFireSystem : ISystem
    {
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<PlayerInputComponent>();
            state.RequireForUpdate<ItemRegistryElement>();
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

            // 롤백 재시뮬레이션에서 같은 발사를 반복하지 않도록 틱당 한 번만 실행.
            // 서버에서는 이 플래그가 항상 켜져 있음.
            if (!networkTime.IsFirstTimeFullyPredictingTick)
            {
                return;
            }

            NativeHashMap<GhostType, int>.ReadOnly registeredGhosts =
                SystemAPI.GetSingleton<GhostCollection>().GhostTypeToColletionIndex;

            Entity registryEntity = SystemAPI.GetSingletonEntity<ItemRegistryElement>();
            DynamicBuffer<ItemRegistryElement> registry = SystemAPI.GetBuffer<ItemRegistryElement>(registryEntity);
            DynamicBuffer<TurretSpecElement> specs = SystemAPI.GetBuffer<TurretSpecElement>(registryEntity);

            float deltaTime = SystemAPI.Time.DeltaTime;
            uint spawnTick = networkTime.ServerTick.SerializedData;

            // 첫 투사체를 만들 때 한 번만 생성. 모든 우주선이 같은 버퍼를 씀.
            var commandBuffer = new EntityCommandBuffer();

            foreach (
                var (transform, inputRequest, movement, owner, delay, equipped, turrets, spaceship) in
                    SystemAPI.Query<
                        RefRO<LocalTransform>,
                        RefRO<PlayerInputComponent>,
                        RefRO<SpaceshipMovementComponent>,
                        RefRO<GhostOwner>,
                        RefRO<CommandDataInterpolationDelay>,
                        RefRO<EquippedTurretComponent>,
                        DynamicBuffer<TurretElement>
                    >().WithAll<Simulate>().WithEntityAccess()
            ) {
                // 기수 방향으로 나아가는 성분만 투사체에 물려줌.
                // 호버링으로 옆으로 흐르는 성분까지 더하면 총구 방향과 어긋난 가속이 붙음.
                float forwardSpeed = math.dot(
                    movement.ValueRO.DeltaMovement,
                    math.normalizesafe(SpaceshipHeading.FromRotation(transform.ValueRO.Rotation), float2.zero)
                );

                // ref 참조를 위해 foreach가 아닌 for로 접근
                for (int i = 0; i < turrets.Length; i++)
                {
                    ref TurretElement turret = ref turrets.ElementAt(i);
                    TurretSpecElement spec = registry.GetTurretSpec(specs, equipped.ValueRO.Value, i);

                    // 투사체가 없는 터렛
                    if (spec.ProjectilePrefab == Entity.Null)
                    {
                        continue;
                    }

                    // 발사 재사용 대기중인 터렛
                    if (turret.FireCooldown > 0.0F)
                    {
                        turret.FireCooldown -= deltaTime;
                        continue;
                    }

                    // 발사 누를시에만 투사체를 발사함.
                    // FireCooldown때문에 Early-exit 불가.
                    if (!inputRequest.ValueRO.TurretTriggered)
                    {
                        continue;
                    }

                    // Ghost Prefab이 등록되기 전에는 예측 스폰한 투사체를 초기화할 수 없음.
                    if (!registeredGhosts.ContainsKey(SystemAPI.GetComponent<GhostType>(spec.ProjectilePrefab)))
                    {
                        continue;
                    }

                    if (!commandBuffer.IsCreated)
                    {
                        commandBuffer = SystemAPI
                            .GetSingleton<EndPredictedSimulationEntityCommandBufferSystem.Singleton>()
                            .CreateCommandBuffer(state.WorldUnmanaged);
                    }

                    // 클라이언트와 서버가 같은 값을 내는 식별자.
                    // 탄퍼짐 시드와 스폰 분류 키를 겸함.
                    uint spawnId = math.hash(new uint3((uint)owner.ValueRO.NetworkId, spawnTick, (uint)i));

                    // 터렛의 로컬 좌표를 월드 좌표로 합성. 원본 Transform은 수정하지 않음.
                    LocalTransform spaceshipTransform = transform.ValueRO;
                    LocalTransform muzzleTransform = spaceshipTransform.TransformTransform(turret.MuzzleLocation);

                    // 탄퍼짐
                    if (spec.Spread > 0.0F)
                    {
                        float halfSpread = spec.Spread * 0.5F;
                        var random = Random.CreateFromIndex(spawnId & 0x7FFFFFFFu);
                        muzzleTransform = muzzleTransform.RotateZ(random.NextFloat(-halfSpread, halfSpread));
                    }

                    Entity projectile = commandBuffer.Instantiate(spec.ProjectilePrefab);
                    commandBuffer.SetComponent(projectile, muzzleTransform);

                    // 투사체가 쏜 우주선과 같은 타임라인에 머물게 함. (OwnerPredicted)
                    commandBuffer.SetComponent(projectile, new GhostOwner { NetworkId = owner.ValueRO.NetworkId });
                    commandBuffer.SetComponent(projectile, new ProjectileSpawnIdComponent { Value = spawnId });

                    // 쏜 우주선의 진영을 물려받음.
                    commandBuffer.SetComponent(projectile, SystemAPI.GetComponent<FactionComponent>(spaceship));

                    // 발사한 우주선과 그 시점의 보간 지연을 기록.
                    // 나머지 값은 프리팹 베이킹 결과를 그대로 씀.
                    var shared = SystemAPI.GetComponent<SharedProjectileComponent>(spec.ProjectilePrefab);
                    shared.Owner = spaceship;
                    shared.TurretIndex = (byte)i;
                    shared.Damage = spec.Damage;
                    shared.InterpolationDelay = delay.ValueRO.Delay;
                    commandBuffer.SetComponent(projectile, shared);

                    // 기수 방향으로 나가는 직진 투사체는 우주선의 속력을 물려받음.
                    if (SystemAPI.HasComponent<StraightforwardProjectileComponent>(spec.ProjectilePrefab))
                    {
                        var straightforward =
                            SystemAPI.GetComponent<StraightforwardProjectileComponent>(spec.ProjectilePrefab);

                        if (straightforward.ReflectOwnerVelocity)
                        {
                            straightforward.Speed += forwardSpeed;
                            commandBuffer.SetComponent(projectile, straightforward);
                        }
                    }

                    turret.FireCooldown = spec.MaxFireCooldown;
                }
            }
        }
    }
}