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
    /// 일정 주기로 플레이어 레이더 안, 시야 밖에 적 우주선을 스폰하는 시스템.
    /// </summary>
    [WorldSystemFilter(WorldSystemFilterFlags.ServerSimulation)]
    public partial struct EnemySpawnSystem : ISystem
    {
        // 스폰 위치 추첨 횟수. 모두 시야 안이면 그 한 대는 건너뜀.
        private const int SpawnAttempts = 8;

        private EntityQuery enemyQuery;

        private double nextSpawnTime;

        public void OnCreate(ref SystemState state)
        {
            enemyQuery = SystemAPI.QueryBuilder().WithAll<EnemyPerceptionComponent>().Build();

            state.RequireForUpdate<NetworkTime>();
            state.RequireForUpdate<PrefabRegistryElement>();
            state.RequireForUpdate<EnemySpawnConfigComponent>();
            state.RequireForUpdate<RelevancyConfigComponent>();

            // 살아 있는 플레이어가 없으면 스폰하지 않음.
            state.RequireForUpdate(
                SystemAPI.QueryBuilder().WithAll<PlayerSpaceshipMovementComponent, LocalTransform>().Build()
            );
        }

        public void OnUpdate(ref SystemState state)
        {
            double elapsed = SystemAPI.Time.ElapsedTime;
            EnemySpawnConfigComponent config = SystemAPI.GetSingleton<EnemySpawnConfigComponent>();

            if (elapsed < nextSpawnTime)
            {
                return;
            }

            nextSpawnTime = elapsed + config.SpawnInterval;

            Entity enemyPrefab = SystemAPI
                .GetSingletonBuffer<PrefabRegistryElement>(true)
                .Find(PrefabId.EnemyStrafingShip);

            // 레지스트리에 적이 없으면 베이킹 설정 문제. 스폰할 것이 없음.
            if (enemyPrefab == Entity.Null)
            {
                return;
            }

            RelevancyConfigComponent relevancy = SystemAPI.GetSingleton<RelevancyConfigComponent>();

            int remaining = config.Cap - enemyQuery.CalculateEntityCount();

            if (remaining <= 0)
            {
                return;
            }

            // 레이더 밖에 스폰하면 바로 제거되므로 최대 거리를 레이더 반경으로 자름.
            float maxDistance = math.min(config.SpawnDistanceRange.y, relevancy.Radius);
            float minDistance = math.min(config.SpawnDistanceRange.x, maxDistance);
            float radiusSq = relevancy.Radius * relevancy.Radius;

            // 시야 중심. 죽은 플레이어는 마지막 위치를 봄.
            var views = new NativeList<float2>(4, Allocator.Temp);

            foreach (
                var position in
                SystemAPI.Query<RefRO<GhostConnectionPosition>>().WithAll<NetworkStreamInGame>()
            ) {
                views.Add(position.ValueRO.Position.xy);
            }

            // 레이더 중심
            var radars = new NativeList<float2>(4, Allocator.Temp);

            foreach (
                var transform in
                SystemAPI.Query<RefRO<LocalTransform>>().WithAll<PlayerSpaceshipMovementComponent>()
            ) {
                radars.Add(transform.ValueRO.Position.xy);
            }

            // 레이더마다 안에 있는 적 수
            var counts = new NativeArray<int>(radars.Length, Allocator.Temp);

            foreach (
                var transform in
                SystemAPI.Query<RefRO<LocalTransform>>().WithAll<EnemyPerceptionComponent>()
            ) {
                float2 xy = transform.ValueRO.Position.xy;

                for (int i = 0; i < radars.Length; i++)
                {
                    if (math.distancesq(radars[i], xy) <= radiusSq)
                    {
                        counts[i]++;
                    }
                }
            }

            uint tick = SystemAPI.GetSingleton<NetworkTime>().ServerTick.SerializedData;
            var random = Random.CreateFromIndex(tick & 0x7FFFFFFFu);

            for (int i = 0; i < radars.Length && remaining > 0; i++)
            {
                int spawnCount = math.min(config.SpawnPerRadar, config.RadarCap - counts[i]);

                for (int n = 0; n < spawnCount && remaining > 0; n++)
                {
                    if (!TryPickPosition(
                            radars[i],
                            minDistance,
                            maxDistance,
                            config.ViewHalfExtents,
                            views,
                            ref random,
                            out float2 position))
                    {
                        continue;
                    }

                    Setup(ref state, state.EntityManager.Instantiate(enemyPrefab), new float3(position, 0.0F), ref random);
                    remaining--;
                }
            }

            views.Dispose();
            radars.Dispose();
            counts.Dispose();
        }

        /// <summary>
        /// 레이더 중심에서 거리 범위 안, 모든 플레이어 시야 밖의 점을 고름.
        /// </summary>
        private static bool TryPickPosition(
            float2 center,
            float minDistance,
            float maxDistance,
            float2 viewHalfExtents,
            NativeList<float2> views,
            ref Random random,
            out float2 position)
        {
            for (int attempt = 0; attempt < SpawnAttempts; attempt++)
            {
                // 면적 기준 균등 분포. 거리를 그대로 뽑으면 안쪽에 몰림.
                float distance = math.sqrt(random.NextFloat(minDistance * minDistance, maxDistance * maxDistance));
                position = center + random.NextFloat2Direction() * distance;

                if (!IsInAnyView(position, viewHalfExtents, views))
                {
                    return true;
                }
            }

            position = float2.zero;

            return false;
        }

        private static bool IsInAnyView(float2 position, float2 halfExtents, NativeList<float2> views)
        {
            for (int i = 0; i < views.Length; i++)
            {
                if (math.all(math.abs(position - views[i]) <= halfExtents))
                {
                    return true;
                }
            }

            return false;
        }

        private void Setup(ref SystemState state, Entity enemy, float3 position, ref Random random)
        {
            // 적 소유자는 -1. 플레이어 NetworkId 는 1부터라 겹치지 않음.
            SystemAPI.SetComponent(enemy, new GhostOwner { NetworkId = -1 });

            SystemAPI.SetComponent(
                enemy,
                LocalTransform.FromPositionRotation(
                    position,
                    SpaceshipHeading.ToRotation(random.NextFloat2Direction())
                )
            );
        }
    }
}
