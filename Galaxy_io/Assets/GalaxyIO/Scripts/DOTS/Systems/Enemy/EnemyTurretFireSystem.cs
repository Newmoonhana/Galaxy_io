using GalaxyIO.DOTS.Components.Actor;
using GalaxyIO.DOTS.Components.Singleton;
using GalaxyIO.DOTS.Spaceship;
using Unity.Entities;
using Unity.Mathematics;
using Unity.NetCode;
using Unity.Transforms;
using Random = Unity.Mathematics.Random;

namespace GalaxyIO.DOTS.Systems
{
    /// <summary>
    /// AI 의 대상을 향해 터렛 투사체를 생성하는 시스템.
    /// 터렛 RPS 위에 <see cref="EnemyTurretComponent"/>의 사격/휴식 주기를 얹음.
    /// 총구 사양은 <see cref="EquippedTurretComponent"/>의 포탑에서 찾음.
    /// </summary>
    [WorldSystemFilter(WorldSystemFilterFlags.ServerSimulation)]
    [UpdateInGroup(typeof(SpawnSystemGroup))]
    public partial struct EnemyTurretFireSystem : ISystem
    {
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<NetworkTime>();
            state.RequireForUpdate<EnemyTurretComponent>();
            state.RequireForUpdate<ItemRegistryElement>();
            state.RequireForUpdate<EndPredictedSimulationEntityCommandBufferSystem.Singleton>();
        }

        public void OnUpdate(ref SystemState state)
        {
            Entity registryEntity = SystemAPI.GetSingletonEntity<ItemRegistryElement>();
            DynamicBuffer<ItemRegistryElement> registry = SystemAPI.GetBuffer<ItemRegistryElement>(registryEntity);
            DynamicBuffer<TurretSpecElement> specs = SystemAPI.GetBuffer<TurretSpecElement>(registryEntity);

            float deltaTime = SystemAPI.Time.DeltaTime;
            uint tick = SystemAPI.GetSingleton<NetworkTime>().ServerTick.SerializedData;

            // 첫 투사체를 만들 때 한 번만 생성. 모든 우주선이 같은 버퍼를 씀.
            var commandBuffer = new EntityCommandBuffer();

            foreach (
                var (transform, movement, perception, enemyTurret, equipped, turrets, spaceship) in
                    SystemAPI.Query<
                        RefRO<LocalTransform>,
                        RefRO<SpaceshipMovementComponent>,
                        RefRO<EnemyPerceptionComponent>,
                        RefRW<EnemyTurretComponent>,
                        RefRO<EquippedTurretComponent>,
                        DynamicBuffer<TurretElement>
                    >().WithAll<Simulate>().WithEntityAccess()
            ) {
                float2 forward = math.normalizesafe(SpaceshipHeading.FromRotation(transform.ValueRO.Rotation), float2.zero);

                bool firing = UpdateFireCycle(
                    ref enemyTurret.ValueRW,
                    perception.ValueRO,
                    transform.ValueRO.Position.xy,
                    forward,
                    deltaTime,
                    math.hash(new uint2((uint)spaceship.Index, tick)) & 0x7FFFFFFFu
                );

                // 기수 방향으로 나아가는 성분만 투사체에 물려줌.
                float forwardSpeed = math.dot(movement.ValueRO.DeltaMovement, forward);

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

                    if (!firing)
                    {
                        continue;
                    }

                    if (!commandBuffer.IsCreated)
                    {
                        commandBuffer = SystemAPI
                            .GetSingleton<EndPredictedSimulationEntityCommandBufferSystem.Singleton>()
                            .CreateCommandBuffer(state.WorldUnmanaged);
                    }

                    LocalTransform muzzleTransform = transform.ValueRO.TransformTransform(turret.MuzzleLocation);

                    // 탄퍼짐
                    if (spec.Spread > 0.0F)
                    {
                        float halfSpread = spec.Spread * 0.5F;
                        var random = Random.CreateFromIndex(
                            math.hash(new uint3((uint)spaceship.Index, tick, (uint)i)) & 0x7FFFFFFFu
                        );
                        muzzleTransform = muzzleTransform.RotateZ(random.NextFloat(-halfSpread, halfSpread));
                    }

                    Entity projectile = commandBuffer.Instantiate(spec.ProjectilePrefab);
                    commandBuffer.SetComponent(projectile, muzzleTransform);

                    // 적 우주선과 같은 소유자 -1. 플레이어 NetworkId(1부터)와 겹치지 않음.
                    // 0 은 "소유자 미지정"으로 취급돼 OwnerPredicted 투사체에 쓸 수 없음.
                    commandBuffer.SetComponent(projectile, new GhostOwner { NetworkId = -1 });

                    // 쏜 우주선의 진영을 물려받음.
                    commandBuffer.SetComponent(projectile, SystemAPI.GetComponent<FactionComponent>(spaceship));

                    // 서버가 직접 쏘므로 되감을 보간 지연이 없음.
                    var shared = SystemAPI.GetComponent<SharedProjectileComponent>(spec.ProjectilePrefab);
                    shared.Owner = spaceship;
                    shared.TurretIndex = (byte)i;
                    shared.Damage = spec.Damage;
                    shared.InterpolationDelay = 0;
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

        /// <summary>
        /// 사격/휴식 주기를 진행하고 이번 틱에 사격할지 반환함.
        /// </summary>
        private static bool UpdateFireCycle(
            ref EnemyTurretComponent enemyTurret,
            in EnemyPerceptionComponent perception,
            float2 position,
            float2 forward,
            float deltaTime,
            uint seed)
        {
            // 대상이 없으면 다음 교전을 사격부터 시작하도록 되돌림.
            if (perception.CurrentTarget == Entity.Null)
            {
                enemyTurret.Resting = false;
                enemyTurret.CycleRemaining = enemyTurret.FireDuration;
                return false;
            }

            enemyTurret.CycleRemaining -= deltaTime;

            if (enemyTurret.CycleRemaining <= 0.0F)
            {
                var random = Random.CreateFromIndex(seed);

                enemyTurret.Resting = !enemyTurret.Resting;
                enemyTurret.CycleRemaining = enemyTurret.Resting
                    ? random.NextFloat(enemyTurret.RestDurationRange.x, enemyTurret.RestDurationRange.y)
                    : enemyTurret.FireDuration;
            }

            if (enemyTurret.Resting)
            {
                return false;
            }

            // 기수가 대상을 향했을 때만 사격.
            float2 toTarget = math.normalizesafe(perception.TargetPosition - position, forward);
            return math.dot(forward, toTarget) >= enemyTurret.AimCosine;
        }
    }
}