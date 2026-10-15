using GalaxyIO.DOTS.Client.Systems;
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
                Proxies    = proxies,
                Cells      = cells,
                CellSize   = config.CellSize,
                Specs      = SystemAPI.GetComponentLookup<SpaceshipSpecComponent>(true),
                Locals     = SystemAPI.GetComponentLookup<LocalProjectileTag>(true),
            }.ScheduleParallel(state.Dependency);

            state.Dependency = proxies.Dispose(state.Dependency);
            state.Dependency = cells.Dispose(state.Dependency);
        }
    }

    /// <summary>
    /// 명중한 투사체의 관통력을 깎고, 다하면 Hidden 으로 표시함. 렌더러는 <see cref="ProjectileVisualSystem"/>이 끔.
    /// Entity 는 끄지 않음. 꺼진 Ghost 는 스냅샷을 받지 못해 명중 수(HitCount)를 놓침.
    /// 한 번 감춘 투사체는 서버가 despawn 할 때까지 다시 보이지 않음.
    /// 클라이언트가 직접 쏜 투사체는 서버 명중 수가 오지 않으므로 여기서 명중 수를 올려 명중 연출을 띄움.
    /// </summary>
    [BurstCompile]
    public partial struct ProjectileHidingJob : IJobEntity
    {
        [ReadOnly]
        public NativeList<ColliderProxy> Proxies;

        [ReadOnly]
        public NativeParallelMultiHashMap<int2, int> Cells;

        public float CellSize;

        [ReadOnly]
        public ComponentLookup<SpaceshipSpecComponent> Specs;

        [ReadOnly]
        public ComponentLookup<LocalProjectileTag> Locals;

        private void Execute(
            Entity entity,
            in LocalTransform transform,
            ref SharedProjectileComponent shared,
            in ColliderComponent collider,
            in FactionComponent faction,
            DynamicBuffer<ProjectilePiercedElement> pierced)
        {
            if (shared.Hidden)
            {
                return;
            }

            // 서버가 명중으로 끝낸 투사체. 클라이언트가 명중을 못 봤어도 감춤.
            if (shared.Dead)
            {
                shared.Hidden = true;
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
                Party    = faction.Party,
                Ignore   = shared.Owner,
            };

            var candidates = new SpatialHashCandidates(Cells, Proxies, CellSize, receiver.Bounds);

            while (candidates.Next(out int i))
            {
                // 서버 판정과 같은 규칙. 자기가 쏜 탄은 발사 즉시 사라지지 않음.
                ColliderProxy target = Proxies[i];

                if (!CollisionFilter.Accepts(receiver, target, CollisionLayer.Spaceship)
                    || pierced.Contains(target.Entity))
                {
                    continue;
                }

                if (!ColliderOverlap.Test(world, target.Collider, out _))
                {
                    continue;
                }

                // 클라이언트가 직접 쏜 투사체. 예측한 명중으로 연출을 띄움(ProjectileHitEffectSystem).
                if (Locals.HasComponent(entity))
                {
                    shared.HitCount++;
                    shared.HitPosition = ColliderOverlap.ClosestOnPath(position - travelled, travelled, target.Collider.Center);
                }

                // 서버와 같은 규칙으로 관통력을 깎음. 남아 있으면 계속 날아감.
                shared.Pierce -= Specs.TryGetComponent(target.Entity, out SpaceshipSpecComponent spec)
                    ? spec.Thickness
                    : 0.0F;

                if (shared.Pierce > 0.0F)
                {
                    pierced.Add(new ProjectilePiercedElement { Spaceship = target.Entity });
                    continue;
                }

                shared.Hidden = true;

                return;
            }
        }
    }
}