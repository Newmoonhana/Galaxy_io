using GalaxyIO.DOTS.Components.Actor;
using GalaxyIO.DOTS.Components.Message;
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
    /// AI(적, 함재기)의 대상을 향해 터렛 투사체 스폰 메시지를 남기는 시스템.
    /// 터렛 RPS 위에 <see cref="EnemyTurretComponent"/>의 사격/휴식 주기를 얹음.
    /// 총구 사양은 <see cref="EquippedTurretComponent"/>의 포탑에서 찾음.
    /// </summary>
    [WorldSystemFilter(WorldSystemFilterFlags.ServerSimulation)]
    [UpdateInGroup(typeof(UpdateSystemGroup))]
    [UpdateAfter(typeof(AISystemGroup))]
    public partial struct EnemyTurretFireSystem : ISystem
    {
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<NetworkTime>();
            state.RequireForUpdate<EnemyTurretComponent>();
            state.RequireForUpdate<ItemRegistryElement>();
            state.RequireForUpdate<ItemConfigComponent>();
        }

        public void OnUpdate(ref SystemState state)
        {
            Entity registryEntity = SystemAPI.GetSingletonEntity<ItemRegistryElement>();
            DynamicBuffer<ItemRegistryElement> registry = SystemAPI.GetBuffer<ItemRegistryElement>(registryEntity);

            float deltaTime = SystemAPI.Time.DeltaTime;
            float standbyTime = SystemAPI.GetSingleton<ItemConfigComponent>().NoFireStandbyTime;
            uint tick = SystemAPI.GetSingleton<NetworkTime>().ServerTick.SerializedData;

            DynamicBuffer<ProjectileSpawnMessage> spawns = SystemAPI.GetSingletonBuffer<ProjectileSpawnMessage>();

            // 우주선마다 비워서 다시 씀.
            var shots = new NativeList<TurretShotRequest>(8, Allocator.Temp);

            foreach (
                var (transform, perception, enemyTurret, equipped, standby, cooldowns, spaceship) in
                    SystemAPI.Query<
                        RefRO<LocalTransform>,
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

                // 사격 시작/중지 전환. 전환 중에는 쏠 수 없음.
                RefRW<TurretSwitchComponent> turretSwitch = SystemAPI.GetComponentRW<TurretSwitchComponent>(spaceship);

                bool shooting = TurretSwitch.Update(
                    ref turretSwitch.ValueRW,
                    firing,
                    registry.Get(equipped.ValueRO.Value).SwitchTime,
                    deltaTime
                );

                TurretSpecData spec = registry.Get(equipped.ValueRO.Value).Turret;

                shots.Clear();
                TurretCycle.Advance(
                    cooldowns.Reinterpret<float>().AsNativeArray(),
                    spec,
                    shooting,
                    deltaTime,
                    ref shots
                );

                // 총구 위치. 쿨다운과 같은 순서.
                DynamicBuffer<TurretElement> muzzles = SystemAPI.GetBuffer<TurretElement>(spaceship);

                // 함재기는 모선의 함재기 강화만큼 세짐.
                float damage = spec.Damage + (SystemAPI.HasComponent<CarriedCraftComponent>(spaceship)
                    ? SystemAPI.GetComponent<CarriedCraftComponent>(spaceship).DamageBonus
                    : 0.0F);

                float speed = spec.ProjectilePrefab != Entity.Null
                              && SystemAPI.HasComponent<StraightforwardProjectileComponent>(spec.ProjectilePrefab)
                    ? SystemAPI.GetComponent<StraightforwardProjectileComponent>(spec.ProjectilePrefab).Speed
                    : 0.0F;

                foreach (TurretShotRequest request in shots)
                {
                    int i = request.Muzzle;

                    for (uint j = 0; j < spec.ProjectileCount; j++)
                    {
                        uint seed = math.hash(new uint4((uint)spaceship.Index, tick, (uint)i, request.Shot * 64 + j));

                        spawns.Add(new ProjectileSpawnMessage
                        {
                            Prefab             = spec.ProjectilePrefab,
                            Spawner            = spaceship,
                            Muzzle             = TurretShot.Aim(muzzles[i].MuzzleLocation, spec, seed, out float speedScale),

                            // 적 우주선과 같은 소유자 -1. 플레이어 NetworkId(1부터)와 겹치지 않음.
                            // 0 은 "소유자 미지정"으로 취급돼 OwnerPredicted 투사체에 쓸 수 없음.
                            NetworkId          = -1,
                            SpawnId            = seed,
                            Turret             = equipped.ValueRO.Value,
                            Damage             = damage,

                            // 서버가 직접 쏘므로 되감을 보간 지연이 없음.
                            InterpolationDelay = 0,
                            Speed              = speed * speedScale,

                            // 기수 방향으로 나아가는 성분만 물려줌.
                            InheritAxis        = new float2(0.0F, 1.0F),
                        });
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

            // 사거리 밖이면 주기를 멈추고 기다림.
            if (math.distance(position, perception.TargetPosition) - perception.TargetThickness * 0.5F > enemyTurret.FireRange)
            {
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
