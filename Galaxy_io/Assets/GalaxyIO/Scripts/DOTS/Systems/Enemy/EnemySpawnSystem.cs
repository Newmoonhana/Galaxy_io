using GalaxyIO.DOTS.Components.Singleton;
using GalaxyIO.DOTS.Spaceship;
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

        private static readonly float3 SpawnPosition = float3.zero;

        private double nextSpawnTime;

        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<NetworkTime>();
            state.RequireForUpdate<PrefabRegistryElement>();

            // 게임에 들어온 플레이어가 없는 서버 월드에서는 스폰하지 않음. 이미 있는 적은 그대로 둠.
            state.RequireForUpdate<NetworkStreamInGame>();
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

            Entity enemy = state.EntityManager.Instantiate(enemyPrefab);

            // 적 소유자는 -1. 플레이어 NetworkId 는 1부터라 겹치지 않음.
            // 적 투사체도 같은 -1 을 써서 적끼리는 서로 맞지 않음. (EnemyTurretFireSystem)
            SystemAPI.SetComponent(enemy, new GhostOwner { NetworkId = -1 });

            // 같은 자리에 스폰되므로 기수 방향을 흩어 서로 갈라지게 함.
            SystemAPI.SetComponent(
                enemy,
                LocalTransform.FromPositionRotation(
                    SpawnPosition,
                    SpaceshipHeading.ToRotation(random.NextFloat2Direction())
                )
            );
        }
    }
}