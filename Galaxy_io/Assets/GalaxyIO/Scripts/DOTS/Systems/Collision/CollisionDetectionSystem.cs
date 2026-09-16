using GalaxyIO.DOTS.Collision;
using GalaxyIO.DOTS.Components.Actor;
using GalaxyIO.DOTS.Components.Singleton;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.NetCode;
using Unity.Transforms;

namespace GalaxyIO.DOTS.Systems
{
    /// <summary>
    /// 충돌체끼리의 겹침을 찾아 메시지로 남기는 시스템. 겹친 뒤의 행동은 소비 시스템이 정함.
    /// 우주선 선체를 공간 해시에 담고, 각 충돌체가 자기 경계 상자가 걸친 칸만 훑음.
    /// </summary>
    [BurstCompile]
    [WorldSystemFilter(WorldSystemFilterFlags.ServerSimulation)]
    [UpdateInGroup(typeof(CollisionDetectionSystemGroup))]
    public partial struct CollisionDetectionSystem : ISystem
    {
        private EntityQuery spaceshipQuery;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            spaceshipQuery = SystemAPI.QueryBuilder()
                .WithAll<SpaceshipSpecComponent, ColliderComponent, FactionComponent, LocalTransform>()
                .Build();

            state.RequireForUpdate<NetworkTime>();
            state.RequireForUpdate<CollisionConfigComponent>();
            state.RequireForUpdate(spaceshipQuery);
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            NetworkTime networkTime = SystemAPI.GetSingleton<NetworkTime>();
            CollisionConfigComponent config = SystemAPI.GetSingleton<CollisionConfigComponent>();
            int count = spaceshipQuery.CalculateEntityCount();
            var proxies = new NativeList<ColliderProxy>(count, Allocator.TempJob);

            // 칸 -> proxies 인덱스. 선체 하나가 여러 칸에 걸침. 해시를 끄면 비워 둠.
            var cells = new NativeParallelMultiHashMap<int2, int>(
                config.UseSpatialHash ? math.max(count * 2, 1) : 1,
                Allocator.TempJob
            );

            state.Dependency = new GatherSpaceshipProxyJob
            {
                Proxies = proxies,
            }.Schedule(state.Dependency);

            if (config.UseSpatialHash)
            {
                state.Dependency = new BuildSpatialHashJob
                {
                    Proxies  = proxies,
                    Cells    = cells,
                    CellSize = config.CellSize,
                }.Schedule(state.Dependency);
            }

            // 지연이 있는 클라이언트의 명중을 되감아 판정하려면 지난 틱 위치가 필요함.
            state.Dependency = new RecordSpaceshipColliderJob
            {
                Tick = networkTime.ServerTick,
            }.ScheduleParallel(state.Dependency);

            // 세 판정은 각자 자기 Entity 의 버퍼에만 쓰므로 함께 돌 수 있음.
            JobHandle spaceship = new SpaceshipCollisionDetectionJob
            {
                Proxies        = proxies,
                Cells          = cells,
                CellSize       = config.CellSize,
                UseSpatialHash = config.UseSpatialHash,
            }.ScheduleParallel(state.Dependency);

            JobHandle projectile = new ProjectileCollisionDetectionJob
            {
                Proxies        = proxies,
                Cells          = cells,
                CellSize       = config.CellSize,
                UseSpatialHash = config.UseSpatialHash,
                Histories      = SystemAPI.GetBufferLookup<SpaceshipColliderHistory>(true),
                RewindSpeed    = config.RewindSpeed,
                DeltaTime      = SystemAPI.Time.DeltaTime,
                CurrentTick    = networkTime.ServerTick,
            }.ScheduleParallel(state.Dependency);

            JobHandle sight = new SightCollisionDetectionJob
            {
                Proxies        = proxies,
                Cells          = cells,
                CellSize       = config.CellSize,
                UseSpatialHash = config.UseSpatialHash,
            }.ScheduleParallel(state.Dependency);

            state.Dependency = JobHandle.CombineDependencies(spaceship, projectile, sight);

            state.Dependency = proxies.Dispose(state.Dependency);
            state.Dependency = cells.Dispose(state.Dependency);
        }
    }

    /// <summary>
    /// 우주선 선체 충돌체를 프록시 배열에 모음.
    /// 표적은 예측 대상이 아닌 우주선도 포함해야 하므로 Simulate로 거르지 않음.
    /// </summary>
    [BurstCompile]
    [WithAll(typeof(SpaceshipSpecComponent))]
    public partial struct GatherSpaceshipProxyJob : IJobEntity
    {
        public NativeList<ColliderProxy> Proxies;

        private void Execute(
            Entity entity,
            in LocalTransform transform,
            in ColliderComponent collider,
            in FactionComponent faction)
        {
            ColliderWorld world = ColliderOverlap.ToWorld(collider, transform);

            Proxies.Add(new ColliderProxy
            {
                Entity   = entity,
                Layer    = CollisionLayer.Spaceship,
                Collider = world,
                Bounds   = ColliderBounds.ToAabb(world),
                Faction  = faction.Value,
                Ignore   = Entity.Null,
            });
        }
    }

    /// <summary>
    /// 우주선 충돌체를 현재 틱 칸에 남김. 되감기 판정에 씀.
    /// </summary>
    [BurstCompile]
    [WithAll(typeof(SpaceshipSpecComponent))]
    public partial struct RecordSpaceshipColliderJob : IJobEntity
    {
        public NetworkTick Tick;

        private void Execute(
            DynamicBuffer<SpaceshipColliderHistory> history,
            in LocalTransform transform,
            in ColliderComponent collider)
        {
            // 링을 처음 한 번만 늘림. 빈 칸의 Tick은 0이고, 유효한 틱은 절대 0이 아님.
            if (history.Length != SpaceshipColliderHistory.Length)
            {
                history.Length = SpaceshipColliderHistory.Length;
            }

            history[(int)(Tick.TickIndexForValidTick % SpaceshipColliderHistory.Length)] =
                new SpaceshipColliderHistory
                {
                    Tick     = Tick.SerializedData,
                    Collider = ColliderOverlap.ToWorld(collider, transform),
                };
        }
    }

    /// <summary>
    /// 선체 - 선체 충돌. 겹친 상대를 자기 버퍼에 남김. 양쪽이 각자 자기 몫을 찾음.
    /// </summary>
    [BurstCompile]
    [WithAll(typeof(SpaceshipSpecComponent))]
    public partial struct SpaceshipCollisionDetectionJob : IJobEntity
    {
        [ReadOnly]
        public NativeList<ColliderProxy> Proxies;

        [ReadOnly]
        public NativeParallelMultiHashMap<int2, int> Cells;

        public float CellSize;
        public bool UseSpatialHash;

        private void Execute(
            Entity entity,
            DynamicBuffer<SpaceshipCollisionElement> messages,
            in LocalTransform transform,
            in ColliderComponent collider,
            in FactionComponent faction)
        {
            messages.Clear();

            ColliderWorld world = ColliderOverlap.ToWorld(collider, transform);

            var receiver = new ColliderProxy
            {
                Entity   = entity,
                Layer    = CollisionLayer.Spaceship,
                Collider = world,
                Bounds   = ColliderBounds.ToAabb(world),
                Faction  = faction.Value,
                Ignore   = Entity.Null,
            };

            var candidates = new SpatialHashCandidates(Cells, Proxies, CellSize, UseSpatialHash, receiver.Bounds);

            while (candidates.Next(out int i))
            {
                ColliderProxy target = Proxies[i];

                if (!CollisionFilter.Accepts(receiver, target, CollisionLayer.Spaceship)
                    || !ColliderOverlap.Test(world, target.Collider, out ColliderHit hit))
                {
                    continue;
                }

                messages.Add(new SpaceshipCollisionElement
                {
                    Message = new CollisionMessage
                    {
                        Other       = target.Entity,
                        OtherCenter = target.Collider.Center,
                        Normal      = hit.Normal,
                        Depth       = hit.Depth,
                    },
                });
            }
        }
    }

    /// <summary>
    /// 투사체 - 선체 충돌. 가장 먼저 맞은 우주선 하나만 남김.
    /// 지연이 있는 클라이언트의 명중은 우주선을 그 시점 위치로 되감아 판정함.
    /// </summary>
    [BurstCompile]
    [WithAll(typeof(Simulate))]
    public partial struct ProjectileCollisionDetectionJob : IJobEntity
    {
        [ReadOnly]
        public NativeList<ColliderProxy> Proxies;

        [ReadOnly]
        public NativeParallelMultiHashMap<int2, int> Cells;

        [ReadOnly]
        public BufferLookup<SpaceshipColliderHistory> Histories;

        public float CellSize;
        public bool UseSpatialHash;

        // 되감는 동안 우주선이 움직일 수 있는 최대 속력. 질의 상자를 이만큼 넓힘.
        public float RewindSpeed;

        public float DeltaTime;
        public NetworkTick CurrentTick;

        private void Execute(
            Entity entity,
            DynamicBuffer<ProjectileHitElement> messages,
            in LocalTransform transform,
            in ColliderComponent collider,
            in FactionComponent faction,
            in SharedProjectileComponent projectile)
        {
            messages.Clear();

            // 한 틱 이동량만큼 늘린 충돌체로 판정. 탄속이 표적보다 커도 통과하지 않음.
            ColliderWorld world = ColliderOverlap.Sweep(
                ColliderOverlap.ToWorld(collider, transform),
                projectile.Displacement
            );

            var receiver = new ColliderProxy
            {
                Entity   = entity,
                Layer    = CollisionLayer.Projectile,
                Collider = world,
                // 되감은 우주선이 후보에서 빠지지 않도록 여유를 둠.
                Bounds   = ColliderBounds.ToAabb(world)
                    .Expand(RewindSpeed * DeltaTime * projectile.InterpolationDelay),
                Faction  = faction.Value,
                Ignore   = projectile.Owner,
            };

            var candidates = new SpatialHashCandidates(Cells, Proxies, CellSize, UseSpatialHash, receiver.Bounds);

            while (candidates.Next(out int i))
            {
                ColliderProxy target = Proxies[i];

                if (!CollisionFilter.Accepts(receiver, target, CollisionLayer.Spaceship))
                {
                    continue;
                }

                // 쏜 사람이 화면에서 본 시점의 위치로 되감음. 클라이언트에서는 지연이 0이라 그대로 씀.
                ColliderWorld rewound = target.Collider;
                TryRewind(target.Entity, projectile.InterpolationDelay, ref rewound);

                if (!ColliderOverlap.Test(world, rewound, out ColliderHit hit))
                {
                    continue;
                }

                messages.Add(new ProjectileHitElement
                {
                    Message = new CollisionMessage
                    {
                        Other       = target.Entity,
                        OtherCenter = rewound.Center,
                        Normal      = hit.Normal,
                        Depth       = hit.Depth,
                    },
                });

                return;
            }
        }

        /// <summary>
        /// 기록해 둔 지난 틱 충돌체로 바꿔치기함. 기록이 없으면 현재 위치를 그대로 둠.
        /// </summary>
        private void TryRewind(Entity spaceship, uint delay, ref ColliderWorld collider)
        {
            if (delay == 0)
            {
                return;
            }

            NetworkTick tick = CurrentTick;
            tick.Subtract(delay);

            if (!Histories.TryGetBuffer(spaceship, out DynamicBuffer<SpaceshipColliderHistory> history)
                || history.Length != SpaceshipColliderHistory.Length)
            {
                return;
            }

            SpaceshipColliderHistory entry =
                history[(int)(tick.TickIndexForValidTick % SpaceshipColliderHistory.Length)];

            // 링을 한 바퀴 넘겨 덮인 칸은 틱이 맞지 않음. 그때는 되감지 않음.
            if (entry.Tick != tick.SerializedData)
            {
                return;
            }

            collider = entry.Collider;
        }
    }

    /// <summary>
    /// 시야 - 선체 충돌. 시야에 겹친 적대 진영 우주선을 가까운 순으로 남김.
    /// </summary>
    [BurstCompile]
    [WithAll(typeof(Simulate))]
    public partial struct SightCollisionDetectionJob : IJobEntity
    {
        // 한 AI 가 들고 갈 대상 수. 밀집 구간에서 버퍼가 무한정 커지지 않게 함.
        private const int Capacity = 4;

        [ReadOnly]
        public NativeList<ColliderProxy> Proxies;

        [ReadOnly]
        public NativeParallelMultiHashMap<int2, int> Cells;

        public float CellSize;
        public bool UseSpatialHash;

        private void Execute(
            Entity entity,
            DynamicBuffer<SightOverlapElement> messages,
            in LocalTransform transform,
            in EnemyPerceptionComponent perception,
            in FactionComponent faction)
        {
            messages.Clear();

            // 시야도 우주선 크기를 따라 커짐.
            ColliderWorld sight = ColliderOverlap.ToWorld(perception.Sight, transform);

            var receiver = new ColliderProxy
            {
                Entity          = entity,
                Layer           = CollisionLayer.Sight,
                Collider        = sight,
                Bounds          = ColliderBounds.ToAabb(sight),
                Faction         = faction.Value,
                HostileFactions = perception.HostileFactions,
                Ignore          = Entity.Null,
            };

            var candidates = new SpatialHashCandidates(Cells, Proxies, CellSize, UseSpatialHash, receiver.Bounds);

            while (candidates.Next(out int i))
            {
                ColliderProxy target = Proxies[i];

                if (!CollisionFilter.Accepts(receiver, target, CollisionLayer.Spaceship)
                    || !ColliderOverlap.Test(sight, target.Collider, out ColliderHit hit))
                {
                    continue;
                }

                Insert(messages, transform.Position.xy, new CollisionMessage
                {
                    Other       = target.Entity,
                    OtherCenter = target.Collider.Center,
                    Normal      = hit.Normal,
                    Depth       = hit.Depth,
                });
            }
        }

        // 가까운 순으로 Capacity 개까지만 유지함.
        private static void Insert(
            DynamicBuffer<SightOverlapElement> messages,
            float2 position,
            in CollisionMessage message)
        {
            float distanceSq = math.distancesq(position, message.OtherCenter);

            if (messages.Length == Capacity
                && math.distancesq(position, messages[Capacity - 1].Message.OtherCenter) <= distanceSq)
            {
                return;
            }

            if (messages.Length < Capacity)
            {
                messages.Add(default);
            }

            int slot = messages.Length - 1;

            while (slot > 0 && math.distancesq(position, messages[slot - 1].Message.OtherCenter) > distanceSq)
            {
                messages[slot] = messages[slot - 1];
                slot--;
            }

            messages[slot] = new SightOverlapElement { Message = message };
        }
    }
}