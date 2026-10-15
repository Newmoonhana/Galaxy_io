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
    /// 모든 플레이어의 스폰 영역이 닿는 거리 밖에 있는 적 우주선을 제거하는 시스템.
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
                .WithAll<GhostConnectionPosition, RelevancyRadiusComponent, NetworkStreamInGame>()
                .Build();

            state.RequireForUpdate<RelevancyConfigComponent>();
            state.RequireForUpdate<EnemySpawnConfigComponent>();
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

            // 스폰 영역 바깥 원이 최대로 치우쳤을 때 닿는 가장 먼 거리.
            // 레이더 밖 스폰 영역의 적을 지우지 않도록 레이더 반경에 곱함.
            float areaScale = 2.0F * SystemAPI.GetSingleton<EnemySpawnConfigComponent>().SpawnAreaScale - 1.0F;

            // Hysteresis 는 경계를 오가는 적이 바로 사라지는 것 방지.
            float maxRadius = 1.0F;

            foreach (
                var radius in
                SystemAPI.Query<RefRO<RelevancyRadiusComponent>>()
                    .WithAll<GhostConnectionPosition, NetworkStreamInGame>()
            ) {
                maxRadius = math.max(maxRadius, radius.ValueRO.Value * areaScale);
            }

            // 칸 한 변 = 가장 큰 바깥 반경. 3x3 칸만 보면 반경 안의 레이더를 모두 찾음.
            float cellSize = maxRadius + config.Hysteresis;

            // 칸 -> (레이더 중심, 바깥 반경)
            var radars = new NativeParallelMultiHashMap<int2, float3>(
                math.max(connectionQuery.CalculateEntityCount(), 1),
                Allocator.TempJob
            );

            foreach (
                var (position, radius) in
                SystemAPI.Query<RefRO<GhostConnectionPosition>, RefRO<RelevancyRadiusComponent>>()
                    .WithAll<NetworkStreamInGame>()
            ) {
                float2 xy = position.ValueRO.Position.xy;

                radars.Add(SpatialHash.ToCell(xy, cellSize), new float3(xy, radius.ValueRO.Value * areaScale + config.Hysteresis));
            }

            state.Dependency = new EnemyDespawnJob
            {
                Radars        = radars,
                CellSize      = cellSize,
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
        public NativeParallelMultiHashMap<int2, float3> Radars;

        public float CellSize;

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
                            out float3 radar,
                            out NativeParallelMultiHashMapIterator<int2> iterator))
                    {
                        continue;
                    }

                    do
                    {
                        if (math.distancesq(position, radar.xy) <= radar.z * radar.z)
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
