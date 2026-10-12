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
    /// 한 접속의 복제 범위 중심.
    /// </summary>
    public struct RelevancyFocus
    {
        public Entity Connection;
        public int NetworkId;
        public float2 Position;

        // 이 접속의 복제 반경. 레이더 탐지 거리 강화만큼 늘어남.
        public float Radius;
    }

    /// <summary>
    /// 접속마다 근처 Ghost 만 복제 대상으로 등록하는 시스템.
    /// </summary>
    [BurstCompile]
    [WorldSystemFilter(WorldSystemFilterFlags.ServerSimulation)]
    [UpdateInGroup(typeof(SimulationSystemGroup), OrderLast = true)]
    [UpdateAfter(typeof(EndSimulationEntityCommandBufferSystem))]
    [UpdateBefore(typeof(GhostSendSystem))]
    public partial struct GhostRelevancySystem : ISystem
    {
        private EntityQuery connectionQuery;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            connectionQuery = SystemAPI.QueryBuilder()
                .WithAll<GhostConnectionPosition, NetworkId, NetworkStreamInGame>()
                .Build();

            state.RequireForUpdate<GhostRelevancy>();
            state.RequireForUpdate<RelevancyConfigComponent>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            RelevancyConfigComponent config = SystemAPI.GetSingleton<RelevancyConfigComponent>();
            RefRW<GhostRelevancy> relevancy = SystemAPI.GetSingletonRW<GhostRelevancy>();

            relevancy.ValueRW.GhostRelevancyMode = GhostRelevancyMode.SetIsRelevant;

            // 접속 초점을 소유한 우주선 위치로 옮김. 우주선이 없으면 마지막 위치를 유지함.
            var ships = new NativeHashMap<int, float3>(8, Allocator.Temp);

            // NetworkId -> 레이더 탐지 거리 강화로 늘어난 복제 반경
            var ranges = new NativeHashMap<int, float>(8, Allocator.Temp);
            float maxRange = 0.0F;

            foreach (
                var (owner, transform, ship) in SystemAPI.Query<RefRO<GhostOwner>, RefRO<LocalTransform>>()
                    .WithAll<PlayerSpaceshipMovementComponent>()
                    .WithEntityAccess()
            ) {
                ships[owner.ValueRO.NetworkId] = transform.ValueRO.Position;

                if (SystemAPI.HasBuffer<UpgradeParamElement>(ship))
                {
                    float range = Upgrade.Value(
                        SystemAPI.GetBuffer<UpgradeParamElement>(ship),
                        SystemAPI.GetBuffer<UpgradeLevelElement>(ship),
                        UpgradeParam.RadarRange
                    );

                    ranges[owner.ValueRO.NetworkId] = range;
                    maxRange = math.max(maxRange, range);
                }
            }

            foreach (
                var (position, networkId) in SystemAPI.Query<RefRW<GhostConnectionPosition>, RefRO<NetworkId>>()
            ) {
                if (ships.TryGetValue(networkId.ValueRO.Value, out float3 shipPosition))
                {
                    position.ValueRW.Position = shipPosition;
                }
            }

            ships.Dispose();

            // 칸 한 변 = 가장 큰 바깥 반경. 3x3 칸만 보면 반경 안의 접속을 모두 찾음.
            float outerRadius = config.Radius + maxRange + config.Hysteresis;

            var focuses = new NativeParallelMultiHashMap<int2, RelevancyFocus>(
                math.max(connectionQuery.CalculateEntityCount(), 1),
                Allocator.TempJob
            );

            foreach (
                var (position, networkId, connection) in
                SystemAPI.Query<RefRO<GhostConnectionPosition>, RefRO<NetworkId>>()
                    .WithAll<NetworkStreamInGame>()
                    .WithEntityAccess()
            ) {
                float2 xy = position.ValueRO.Position.xy;
                ranges.TryGetValue(networkId.ValueRO.Value, out float range);

                focuses.Add(SpatialHash.ToCell(xy, outerRadius), new RelevancyFocus
                {
                    Connection = connection,
                    NetworkId  = networkId.ValueRO.Value,
                    Position   = xy,
                    Radius     = config.Radius + range,
                });
            }

            ranges.Dispose();

            var relevant = new NativeQueue<RelevantGhostForConnection>(Allocator.TempJob);

            state.Dependency = new CollectRelevantGhostJob
            {
                Focuses       = focuses,
                Previous      = relevancy.ValueRO.GhostRelevancySet,
                Relevant      = relevant.AsParallelWriter(),
                CellSize   = outerRadius,
                Hysteresis = config.Hysteresis,
            }.ScheduleParallel(state.Dependency);

            state.Dependency = new FillRelevancySetJob
            {
                Relevant = relevant,
                Set      = relevancy.ValueRO.GhostRelevancySet,
            }.Schedule(state.Dependency);

            state.Dependency = focuses.Dispose(state.Dependency);
            state.Dependency = relevant.Dispose(state.Dependency);
        }
    }

    /// <summary>
    /// Ghost 마다 주변 칸의 접속을 찾아 반경 안이면 쌍으로 남김.
    /// </summary>
    [BurstCompile]
    public partial struct CollectRelevantGhostJob : IJobEntity
    {
        [ReadOnly]
        public NativeParallelMultiHashMap<int2, RelevancyFocus> Focuses;

        // 직전 프레임에 보낸 쌍
        [ReadOnly]
        public NativeParallelHashMap<RelevantGhostForConnection, int> Previous;

        public NativeQueue<RelevantGhostForConnection>.ParallelWriter Relevant;

        public float CellSize;
        public float Hysteresis;

        private void Execute(in GhostInstance ghost, in LocalTransform transform)
        {
            // 아직 id 를 받지 못한 갓 스폰한 Ghost. 다음 프레임에 등록됨.
            if (ghost.ghostId == 0)
            {
                return;
            }

            float2 position = transform.Position.xy;
            int2 center = SpatialHash.ToCell(position, CellSize);

            for (int y = -1; y <= 1; y++)
            {
                for (int x = -1; x <= 1; x++)
                {
                    if (!Focuses.TryGetFirstValue(
                            center + new int2(x, y),
                            out RelevancyFocus focus,
                            out NativeParallelMultiHashMapIterator<int2> iterator))
                    {
                        continue;
                    }

                    do
                    {
                        float distanceSq = math.distancesq(position, focus.Position);
                        float outerRadius = focus.Radius + Hysteresis;

                        if (distanceSq > outerRadius * outerRadius)
                        {
                            continue;
                        }

                        var pair = new RelevantGhostForConnection(focus.NetworkId, ghost.ghostId);

                        // 경계에서 깜빡이지 않도록 보내던 Ghost 는 바깥 반경까지 유지함.
                        if (distanceSq <= focus.Radius * focus.Radius || Previous.ContainsKey(pair))
                        {
                            Relevant.Enqueue(pair);
                        }
                    }
                    while (Focuses.TryGetNextValue(out focus, ref iterator));
                }
            }
        }
    }

    /// <summary>
    /// 모은 쌍으로 복제 대상 집합을 새로 채움.
    /// </summary>
    [BurstCompile]
    public struct FillRelevancySetJob : IJob
    {
        public NativeQueue<RelevantGhostForConnection> Relevant;
        public NativeParallelHashMap<RelevantGhostForConnection, int> Set;

        public void Execute()
        {
            Set.Clear();

            if (Set.Capacity < Relevant.Count)
            {
                Set.Capacity = Relevant.Count;
            }

            while (Relevant.TryDequeue(out RelevantGhostForConnection pair))
            {
                Set.TryAdd(pair, 1);
            }
        }
    }
}