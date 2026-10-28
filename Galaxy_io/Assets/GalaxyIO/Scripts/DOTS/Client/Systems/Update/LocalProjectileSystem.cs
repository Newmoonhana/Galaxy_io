using GalaxyIO.DOTS.Components.Actor;
using GalaxyIO.DOTS.Components.Singleton;
using GalaxyIO.DOTS.Spaceship;
using GalaxyIO.DOTS.Systems;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace GalaxyIO.DOTS.Client.Systems
{
    /// <summary>
    /// Ghost 가 아닌 투사체를 클라이언트가 직접 발사하고 움직이는 시스템.
    /// 서버는 같은 투사체를 복제하지 않고 데미지 판정에만 씀. 클라이언트는 복제된 사격 전환 상태만 보고
    /// 모든 우주선(자기 우주선 포함)의 투사체를 따로 쏨. 연출 전용이라 서버와 탄 위치가 다를 수 있음.
    /// 자동 포탑은 복제된 사격 여부(<see cref="AutoTurretElement.Firing"/>)를 보고 쏨.
    /// e.g. Pyron(화염방사기)
    /// </summary>
    [BurstCompile]
    [WorldSystemFilter(WorldSystemFilterFlags.ClientSimulation)]
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateBefore(typeof(TransformSystemGroup))]
    [UpdateBefore(typeof(ProjectileVisualSystem))]
    public partial struct LocalProjectileSystem : ISystem
    {
        private EntityQuery steeringQuery;
        private EntityQuery movingQuery;
        private EntityQuery lifetimeQuery;

        // 탄퍼짐 시드용 프레임 번호. 클라이언트끼리 같을 필요 없음.
        private uint frame;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            steeringQuery = SystemAPI.QueryBuilder()
                .WithAll<LocalProjectileTag, LocalTransform>()
                .WithAllRW<StraightforwardProjectileComponent, SharedProjectileComponent>()
                .WithAllRW<MovementComponent>()
                .Build();

            movingQuery = SystemAPI.QueryBuilder()
                .WithAll<LocalProjectileTag, MovementComponent>()
                .WithAllRW<LocalTransform>()
                .Build();

            lifetimeQuery = SystemAPI.QueryBuilder()
                .WithAll<LocalProjectileTag>()
                .WithAllRW<SharedProjectileComponent>()
                .Build();

            state.RequireForUpdate<ItemRegistryElement>();
            state.RequireForUpdate<EndSimulationEntityCommandBufferSystem.Singleton>();
            state.RequireForUpdate<LocalLaunchComponent>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            float deltaTime = SystemAPI.Time.DeltaTime;
            frame++;

            Entity registryEntity = SystemAPI.GetSingletonEntity<ItemRegistryElement>();
            DynamicBuffer<ItemRegistryElement> registry = SystemAPI.GetBuffer<ItemRegistryElement>(registryEntity);

            var ecb = new EntityCommandBuffer(Allocator.Temp);

            // 우주선마다 비워서 다시 씀.
            var shots = new NativeList<TurretShotRequest>(8, Allocator.Temp);

            // TurretCycle 에 넘길 자동 포탑 총구 하나의 쿨다운.
            var autoCooldown = new NativeArray<float>(1, Allocator.Temp);

            foreach (
                var (transform, turretSwitch, equipped, faction, launch, cooldowns, spaceship) in
                SystemAPI.Query<
                    RefRO<LocalTransform>,
                    RefRO<TurretSwitchComponent>,
                    RefRO<EquippedTurretComponent>,
                    RefRO<FactionComponent>,
                    RefRW<LocalLaunchComponent>,
                    DynamicBuffer<LocalLaunchCooldownElement>
                >().WithEntityAccess()
            ) {
                // 다른 우주선의 속도는 복제되지 않으므로 위치 변화로 구함.
                float2 position = transform.ValueRO.Position.xy;
                float2 velocity = launch.ValueRO.Tracked && deltaTime > 0.0F
                    ? (position - launch.ValueRO.LastPosition) / deltaTime
                    : float2.zero;

                launch.ValueRW.LastPosition = position;
                launch.ValueRW.Velocity = velocity;
                launch.ValueRW.Tracked = true;

                // 기수 방향으로 나아가는 성분만 투사체에 물려줌.
                float forwardSpeed = math.dot(
                    velocity,
                    math.normalizesafe(SpaceshipHeading.FromRotation(transform.ValueRO.Rotation), float2.zero)
                );

                bool firing = turretSwitch.ValueRO.State == TurretSwitchState.Firing;
                DynamicBuffer<TurretElement> muzzles = SystemAPI.GetBuffer<TurretElement>(spaceship);

                if (cooldowns.Length != muzzles.Length)
                {
                    cooldowns.Resize(muzzles.Length, NativeArrayOptions.ClearMemory);
                }

                TurretSpecData spec = registry.Get(equipped.ValueRO.Value).Turret;

                shots.Clear();
                TurretCycle.Advance(
                    cooldowns.Reinterpret<float>().AsNativeArray(),
                    spec,
                    firing,
                    deltaTime,
                    ref shots
                );

                // 자동 포탑의 발은 총구 번호를 조종 포탑 총구 뒤에 이어 붙임.
                DynamicBuffer<AutoTurretElement> autoTurrets = SystemAPI.GetBuffer<AutoTurretElement>(spaceship);
                DynamicBuffer<AutoTurretMountElement> mounts = SystemAPI.GetBuffer<AutoTurretMountElement>(spaceship);

                for (int k = 0; k < autoTurrets.Length; k++)
                {
                    TurretSpecData autoSpec = registry.Get(autoTurrets[k].Turret).Turret;

                    if (!autoSpec.LocalLaunch)
                    {
                        continue;
                    }

                    int start = shots.Length;

                    autoCooldown[0] = mounts[k].Cooldown;
                    TurretCycle.Advance(autoCooldown, autoSpec, autoTurrets[k].Firing, deltaTime, ref shots);
                    mounts.ElementAt(k).Cooldown = autoCooldown[0];

                    for (int s = start; s < shots.Length; s++)
                    {
                        shots.ElementAt(s).Muzzle = muzzles.Length + k;
                    }
                }

                foreach (TurretShotRequest request in shots)
                {
                    int i = request.Muzzle;
                    int auto = i - muzzles.Length;
                    TurretSpecData shotSpec = auto >= 0 ? registry.Get(autoTurrets[auto].Turret).Turret : spec;

                    // Ghost 투사체는 서버, 예측 발사가 냄.
                    if (!shotSpec.LocalLaunch)
                    {
                        continue;
                    }

                    LocalTransform muzzleTransform = transform.ValueRO.TransformTransform(
                        auto >= 0
                            ? AutoTurret.MuzzleLocation(mounts[auto], mounts[auto].VisualAngle)
                            : muzzles[i].MuzzleLocation
                    );

                    // 자동 포탑은 총구 방향으로 나아가는 성분을 물려줌.
                    float inheritedSpeed = auto >= 0 ? math.dot(velocity, muzzleTransform.Up().xy) : forwardSpeed;

                    var shared = SystemAPI.GetComponent<SharedProjectileComponent>(shotSpec.ProjectilePrefab);
                    shared.Owner = spaceship;
                    shared.Damage = shotSpec.Damage;

                    bool straight = SystemAPI.HasComponent<StraightforwardProjectileComponent>(shotSpec.ProjectilePrefab);
                    StraightforwardProjectileComponent straightforward = straight
                        ? SystemAPI.GetComponent<StraightforwardProjectileComponent>(shotSpec.ProjectilePrefab)
                        : default;

                    for (uint j = 0; j < shotSpec.ProjectileCount; j++)
                    {
                        uint seed = math.hash(new uint4((uint)spaceship.Index, frame, (uint)i, request.Shot * 64 + j));

                        Entity projectile = ecb.Instantiate(shotSpec.ProjectilePrefab);
                        ecb.SetComponent(projectile, TurretShot.Aim(muzzleTransform, shotSpec, seed, out float speedScale));
                        ecb.SetComponent(projectile, faction.ValueRO);
                        ecb.SetComponent(projectile, shared);
                        ecb.AddComponent<LocalProjectileTag>(projectile);

                        if (straight)
                        {
                            StraightforwardProjectileComponent launched = straightforward;
                            launched.Speed = straightforward.Speed * speedScale
                                             + (straightforward.ReflectOwnerVelocity ? inheritedSpeed : 0.0F);
                            ecb.SetComponent(projectile, launched);
                        }
                    }
                }
            }

            ecb.Playback(state.EntityManager);
            ecb.Dispose();

            // 쏜 투사체를 프레임마다 한 번 움직이고 수명이 다하면 지움. 메인 스레드 작업 뒤에 예약함.
            state.Dependency = new StraightforwardProjectileJob
            {
                DeltaTime = deltaTime,
            }.ScheduleParallel(steeringQuery, state.Dependency);

            state.Dependency = new MovementJob
            {
                DeltaTime = deltaTime,
            }.ScheduleParallel(movingQuery, state.Dependency);

            state.Dependency = new SharedProjectileJob
            {
                DeltaTime = deltaTime,
                Authoritative = true,
                CommandBuffer = SystemAPI
                    .GetSingleton<EndSimulationEntityCommandBufferSystem.Singleton>()
                    .CreateCommandBuffer(state.WorldUnmanaged).AsParallelWriter(),
            }.ScheduleParallel(lifetimeQuery, state.Dependency);
        }
    }
}