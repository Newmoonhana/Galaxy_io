using GalaxyIO.DOTS.Collision;
using GalaxyIO.DOTS.Components.Actor;
using GalaxyIO.DOTS.Components.Singleton;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.NetCode;
using Unity.Transforms;

namespace GalaxyIO.DOTS.Systems
{
    /// <summary>
    /// 모든 플레이어 레이더 밖에 있는 적 우주선을 제거하는 시스템.
    /// </summary>
    [BurstCompile]
    [WorldSystemFilter(WorldSystemFilterFlags.ServerSimulation)]
    [UpdateInGroup(typeof(DespawnSystemGroup))]
    [UpdateAfter(typeof(SpaceshipDeathSystem))]
    public partial struct EnemyDespawnSystem : ISystem
    {
        private EntityQuery connectionQuery;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            connectionQuery = SystemAPI.QueryBuilder()
                .WithAll<GhostConnectionPosition, NetworkStreamInGame>()
                .Build();

            state.RequireForUpdate<RelevancyConfigComponent>();
            state.RequireForUpdate<EndPredictedSimulationEntityCommandBufferSystem.Singleton>();
            state.RequireForUpdate(
                SystemAPI.QueryBuilder()
                    .WithAll<EnemyPerceptionComponent, LocalTransform>()
                    .WithNone<PermanentSpaceshipTag>()
                    .Build()
            );
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            RelevancyConfigComponent config = SystemAPI.GetSingleton<RelevancyConfigComponent>();

            // Ghost 복제 범위와 같은 바깥 반경. 경계를 오가는 적이 바로 사라지는 것 방지.
            float outerRadius = config.Radius + config.Hysteresis;

            // 칸 한 변 = 바깥 반경. 3x3 칸만 보면 반경 안의 레이더를 모두 찾음.
            var radars = new NativeParallelMultiHashMap<int2, float2>(
                math.max(connectionQuery.CalculateEntityCount(), 1),
                Allocator.TempJob
            );

            foreach (
                var position in
                SystemAPI.Query<RefRO<GhostConnectionPosition>>().WithAll<NetworkStreamInGame>()
            ) {
                float2 xy = position.ValueRO.Position.xy;

                radars.Add(SpatialHash.ToCell(xy, outerRadius), xy);
            }

            state.Dependency = new EnemyDespawnJob
            {
                Radars        = radars,
                CellSize      = outerRadius,
                OuterRadiusSq = outerRadius * outerRadius,
                CommandBuffer = SystemAPI
                    .GetSingleton<EndPredictedSimulationEntityCommandBufferSystem.Singleton>()
                    .CreateCommandBuffer(state.WorldUnmanaged).AsParallelWriter(),
            }.ScheduleParallel(state.Dependency);

            state.Dependency = radars.Dispose(state.Dependency);
        }
    }

    /// <summary>
    /// 주변 칸의 레이더를 찾아 반경 안에 하나도 없으면 제거함. 크기는 보지 않고 중심점만 판정함.
    /// </summary>
    [BurstCompile]
    [WithAll(typeof(EnemyPerceptionComponent))]
    [WithNone(typeof(PermanentSpaceshipTag))]
    public partial struct EnemyDespawnJob : IJobEntity
    {
        [ReadOnly]
        public NativeParallelMultiHashMap<int2, float2> Radars;

        public float CellSize;
        public float OuterRadiusSq;

        public EntityCommandBuffer.ParallelWriter CommandBuffer;

        private void Execute(Entity entity, [ChunkIndexInQuery] int chunkIndex, in LocalTransform transform)
        {
            float2 position = transform.Position.xy;
            int2 center = SpatialHash.ToCell(position, CellSize);

            for (int y = -1; y <= 1; y++)
            {
                for (int x = -1; x <= 1; x++)
                {
                    if (!Radars.TryGetFirstValue(
                            center + new int2(x, y),
                            out float2 radar,
                            out NativeParallelMultiHashMapIterator<int2> iterator))
                    {
                        continue;
                    }

                    do
                    {
                        if (math.distancesq(position, radar) <= OuterRadiusSq)
                        {
                            return;
                        }
                    }
                    while (Radars.TryGetNextValue(out radar, ref iterator));
                }
            }

            CommandBuffer.DestroyEntity(chunkIndex, entity);
        }
    }
}
