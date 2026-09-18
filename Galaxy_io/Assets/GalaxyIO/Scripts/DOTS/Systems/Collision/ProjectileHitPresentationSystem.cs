using GalaxyIO.DOTS.Collision;
using GalaxyIO.DOTS.Components.Actor;
using GalaxyIO.DOTS.Components.Singleton;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Transforms;

namespace GalaxyIO.DOTS.Systems
{
    /// <summary>
    /// 명중한 것으로 보이는 투사체를 화면에서 감추는 클라이언트 전용 시스템.
    /// 서버 판정이 도착하기 전까지 탄이 표적을 지나쳐 날아가는 것을 막음.
    /// </summary>
    [BurstCompile]
    [WorldSystemFilter(WorldSystemFilterFlags.ClientSimulation)]
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    public partial struct ProjectileHitPresentationSystem : ISystem
    {
        private EntityQuery spaceshipQuery;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            spaceshipQuery = SystemAPI.QueryBuilder()
                .WithAll<SpaceshipSpecComponent, ColliderComponent, FactionComponent, LocalTransform>()
                .Build();

            state.RequireForUpdate<EndSimulationEntityCommandBufferSystem.Singleton>();
            state.RequireForUpdate<CollisionConfigComponent>();
            state.RequireForUpdate(SystemAPI.QueryBuilder().WithAll<SharedProjectileComponent>().Build());
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            CollisionConfigComponent config = SystemAPI.GetSingleton<CollisionConfigComponent>();
            int count = spaceshipQuery.CalculateEntityCount();

            var proxies = new NativeList<ColliderProxy>(count, Allocator.TempJob);

            // 칸 -> proxies 인덱스
            var cells = new NativeParallelMultiHashMap<int2, int>(math.max(count * 2, 1), Allocator.TempJob);

            // Spaceship Collider 수집
            state.Dependency = new GatherSpaceshipProxyJob
            {
                Proxies = proxies,
            }.Schedule(state.Dependency);

            state.Dependency = new BuildSpatialHashJob
            {
                Proxies  = proxies,
                Cells    = cells,
                CellSize = config.CellSize,
            }.Schedule(state.Dependency);

            state.Dependency = new ProjectileHidingJob
            {
                Proxies  = proxies,
                Cells    = cells,
                CellSize = config.CellSize,
                CommandBuffer = SystemAPI
                    .GetSingleton<EndSimulationEntityCommandBufferSystem.Singleton>()
                    .CreateCommandBuffer(state.WorldUnmanaged).AsParallelWriter(),
            }.ScheduleParallel(state.Dependency);

            state.Dependency = proxies.Dispose(state.Dependency);
            state.Dependency = cells.Dispose(state.Dependency);
        }
    }

    /// <summary>
    /// 명중한 투사체를 화면에서 지움.
    /// <see cref="Disabled"/>가 붙으면 Entities가 스프라이트를 들고 있는 GameObject를 꺼 줌.
    /// 한 번 지운 투사체는 서버가 despawn 할 때까지 다시 보이지 않음.
    /// </summary>
    [BurstCompile]
    public partial struct ProjectileHidingJob : IJobEntity
    {
        [ReadOnly]
        public NativeList<ColliderProxy> Proxies;

        [ReadOnly]
        public NativeParallelMultiHashMap<int2, int> Cells;

        public float CellSize;

        public EntityCommandBuffer.ParallelWriter CommandBuffer;

        private void Execute(
            Entity entity,
            [ChunkIndexInQuery] int chunkIndex,
            in LocalTransform transform,
            ref SharedProjectileComponent shared,
            in ColliderComponent collider,
            in FactionComponent faction)
        {
            if (shared.Hidden)
            {
                return;
            }

            float2 position = transform.Position.xy;
            float2 travelled = position - shared.LastTestedPosition;
            bool first = !shared.Tested;

            shared.LastTestedPosition = position;
            shared.Tested = true;

            // 갓 스폰한 투사체는 비교할 직전 위치가 없음.
            if (first)
            {
                return;
            }

            // 프레임 사이에 지나간 구간 전체를 훑음. 표적을 건너뛰지 않음.
            ColliderWorld world = ColliderOverlap.Sweep(
                ColliderOverlap.ToWorld(collider, transform),
                travelled
            );

            var receiver = new ColliderProxy
            {
                Entity   = entity,
                Layer    = CollisionLayer.Projectile,
                Collider = world,
                Bounds   = ColliderBounds.ToAabb(world),
                Faction  = faction.Value,
                Ignore   = shared.Owner,
            };

            var candidates = new SpatialHashCandidates(Cells, Proxies, CellSize, receiver.Bounds);

            while (candidates.Next(out int i))
            {
                // 서버 판정과 같은 규칙. 자기가 쏜 탄은 발사 즉시 사라지지 않음.
                if (!CollisionFilter.Accepts(receiver, Proxies[i], CollisionLayer.Spaceship))
                {
                    continue;
                }

                if (!ColliderOverlap.Test(world, Proxies[i].Collider, out _))
                {
                    continue;
                }

                shared.Hidden = true;
                CommandBuffer.AddComponent<Disabled>(chunkIndex, entity);

                return;
            }
        }
    }
}