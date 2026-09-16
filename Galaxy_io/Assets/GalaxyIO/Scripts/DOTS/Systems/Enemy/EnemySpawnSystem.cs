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
    /// 일정 주기로 적 우주선을 스폰하는 시스템.
    /// </summary>
    [WorldSystemFilter(WorldSystemFilterFlags.ServerSimulation)]
    public partial struct EnemySpawnSystem : ISystem
    {
        // 스폰 주기(초)
        private const float SpawnInterval = 5.0F;

        // 스트레스 테스트 격자 범위. [-StressTestRange, StressTestRange)
        private const float StressTestRange = 500.0F;

        // 스트레스 테스트 격자 간격
        private const float StressTestSpacing = 50.0F;

        private static readonly float3 SpawnPosition = float3.zero;

        private EntityQuery enemyQuery;
        private EntityQuery inGameQuery;

        private double nextSpawnTime;

        public void OnCreate(ref SystemState state)
        {
            enemyQuery = SystemAPI.QueryBuilder().WithAll<EnemyPerceptionComponent>().Build();
            inGameQuery = SystemAPI.QueryBuilder().WithAll<NetworkStreamInGame>().Build();

            state.RequireForUpdate<NetworkTime>();
            state.RequireForUpdate<PrefabRegistryElement>();
            state.RequireForUpdate<EnemySpawnConfigComponent>();
        }

        public void OnUpdate(ref SystemState state)
        {
            double elapsed = SystemAPI.Time.ElapsedTime;

            if (elapsed < nextSpawnTime)
            {
                return;
            }

            nextSpawnTime = elapsed + SpawnInterval;

            Entity enemyPrefab = SystemAPI
                .GetSingletonBuffer<PrefabRegistryElement>(true)
                .Find(PrefabId.EnemyStrafingShip);

            // 레지스트리에 적이 없으면 베이킹 설정 문제. 스폰할 것이 없음.
            if (enemyPrefab == Entity.Null)
            {
                return;
            }

            uint tick = SystemAPI.GetSingleton<NetworkTime>().ServerTick.SerializedData;
            var random = Random.CreateFromIndex(tick & 0x7FFFFFFFu);

            EnemySpawnConfigComponent config = SystemAPI.GetSingleton<EnemySpawnConfigComponent>();

            // 게임에 들어온 플레이어가 없는 서버 월드에서는 스폰하지 않음.
            // 스트레스 테스트는 접속 없이 관찰해야 해서 예외임.
            if (!config.StressTest && inGameQuery.IsEmpty)
            {
                // 임시로 꺼놓음. Spatial Hash 의 서버 성능 측정을 위해.
                // return;
            }

            int remaining = config.Cap - enemyQuery.CalculateEntityCount();

            if (remaining <= 0)
            {
                return;
            }

            if (config.StressTest)
            {
                SpawnGrid(ref state, enemyPrefab, remaining, ref random);

                return;
            }

            Setup(ref state, state.EntityManager.Instantiate(enemyPrefab), SpawnPosition, ref random);
        }

        /// <summary>
        /// 격자점마다 한 대씩 스폰함. 상한에 걸리면 걸린 만큼만 스폰함.
        /// </summary>
        private void SpawnGrid(ref SystemState state, Entity enemyPrefab, int remaining, ref Random random)
        {
            int side = (int)(StressTestRange * 2.0F / StressTestSpacing);
            NativeArray<Entity> enemies = state.EntityManager.Instantiate(
                enemyPrefab,
                math.min(side * side, remaining),
                Allocator.Temp
            );

            for (int i = 0; i < enemies.Length; i++)
            {
                var position = new float3(
                    -StressTestRange + i % side * StressTestSpacing,
                    -StressTestRange + i / side * StressTestSpacing,
                    0.0F
                );

                Setup(ref state, enemies[i], position, ref random);
            }

            enemies.Dispose();
        }

        private void Setup(ref SystemState state, Entity enemy, float3 position, ref Random random)
        {
            // 적 소유자는 -1. 플레이어 NetworkId 는 1부터라 겹치지 않음.
            SystemAPI.SetComponent(enemy, new GhostOwner { NetworkId = -1 });

            // 같은 자리에 스폰될 수 있으므로 기수 방향을 흩어 서로 갈라지게 함.
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