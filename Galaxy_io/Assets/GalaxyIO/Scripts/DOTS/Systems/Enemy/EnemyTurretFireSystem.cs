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
            state.RequireForUpdate<ItemConfigComponent>();
            state.RequireForUpdate<EndPredictedSimulationEntityCommandBufferSystem.Singleton>();
        }

        public void OnUpdate(ref SystemState state)
        {
            Entity registryEntity = SystemAPI.GetSingletonEntity<ItemRegistryElement>();
            DynamicBuffer<ItemRegistryElement> registry = SystemAPI.GetBuffer<ItemRegistryElement>(registryEntity);
            DynamicBuffer<TurretSpecElement> specs = SystemAPI.GetBuffer<TurretSpecElement>(registryEntity);

            float deltaTime = SystemAPI.Time.DeltaTime;
            float standbyTime = SystemAPI.GetSingleton<ItemConfigComponent>().NoFireStandbyTime;
            uint tick = SystemAPI.GetSingleton<NetworkTime>().ServerTick.SerializedData;

            // 첫 투사체를 만들 때 한 번만 생성. 모든 우주선이 같은 버퍼를 씀.
            var commandBuffer = new EntityCommandBuffer();

            // 우주선마다 비워서 다시 씀.
            var shots = new NativeList<TurretShotRequest>(8, Allocator.Temp);

            foreach (
                var (transform, movement, perception, enemyTurret, equipped, standby, cooldowns, spaceship) in
                    SystemAPI.Query<
                        RefRO<LocalTransform>,
                        RefRO<SpaceshipMovementComponent>,
                        RefRO<EnemyPerceptionComponent>,
                        RefRW<EnemyTurretComponent>,
                        RefRO<EquippedTurretComponent>,
                        RefRW<TurretStandbyComponent>,
                        DynamicBuffer<TurretCooldownElement>
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

                // 사격 시작/중지 전환. 전환 중에는 쏠 수 없음.
                RefRW<TurretSwitchComponent> turretSwitch = SystemAPI.GetComponentRW<TurretSwitchComponent>(spaceship);

                bool shooting = TurretSwitch.Update(
                    ref turretSwitch.ValueRW,
                    firing,
                    registry.Get(equipped.ValueRO.Value).SwitchTime,
                    deltaTime
                );

                shots.Clear();
                TurretCycle.Advance(
                    cooldowns.Reinterpret<float>().AsNativeArray(),
                    registry,
                    specs,
                    equipped.ValueRO.Value,
                    shooting,
                    deltaTime,
                    ref shots
                );

                // 총구 위치. 쿨다운과 같은 순서.
                DynamicBuffer<TurretElement> muzzles = SystemAPI.GetBuffer<TurretElement>(spaceship);

                foreach (TurretShotRequest request in shots)
                {
                    int i = request.Muzzle;
                    TurretSpecElement spec = registry.GetTurretSpec(specs, equipped.ValueRO.Value, i);

                    if (!commandBuffer.IsCreated)
                    {
                        commandBuffer = SystemAPI
                            .GetSingleton<EndPredictedSimulationEntityCommandBufferSystem.Singleton>()
                            .CreateCommandBuffer(state.WorldUnmanaged);
                    }

                    LocalTransform muzzleTransform = transform.ValueRO.TransformTransform(muzzles[i].MuzzleLocation);

                    // 서버가 직접 쏘므로 되감을 보간 지연이 없음.
                    var shared = SystemAPI.GetComponent<SharedProjectileComponent>(spec.ProjectilePrefab);
                    shared.Owner = spaceship;
                    shared.TurretIndex = (byte)i;
                    shared.Damage = spec.Damage;
                    shared.InterpolationDelay = 0;

                    bool straight = SystemAPI.HasComponent<StraightforwardProjectileComponent>(spec.ProjectilePrefab);
                    StraightforwardProjectileComponent straightforward = straight
                        ? SystemAPI.GetComponent<StraightforwardProjectileComponent>(spec.ProjectilePrefab)
                        : default;

                    for (uint j = 0; j < spec.ProjectileCount; j++)
                    {
                        uint seed = math.hash(new uint4((uint)spaceship.Index, tick, (uint)i, request.Shot * 64 + j));

                        Entity projectile = commandBuffer.Instantiate(spec.ProjectilePrefab);
                        commandBuffer.SetComponent(
                            projectile,
                            TurretShot.Aim(muzzleTransform, spec, seed, out float speedScale)
                        );

                        // 적 우주선과 같은 소유자 -1. 플레이어 NetworkId(1부터)와 겹치지 않음.
                        // 0 은 "소유자 미지정"으로 취급돼 OwnerPredicted 투사체에 쓸 수 없음.
                        commandBuffer.SetComponent(projectile, new GhostOwner { NetworkId = -1 });

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

                TurretStandby.Update(ref standby.ValueRW, !shots.IsEmpty, deltaTime, standbyTime);
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