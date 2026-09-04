using GalaxyIO.DOTS.Components.Projectiles;
using Unity.Burst;
using Unity.Entities;
using Unity.NetCode;
using Unity.NetCode.LowLevel;

namespace GalaxyIO.DOTS.Client.Systems
{
    /// <summary>
    /// 서버가 보낸 투사체 Ghost를 클라이언트가 예측 스폰해둔 투사체와 이어붙이는 시스템.
    /// <see cref="ProjectileSpawnIdComponent"/>가 같은 것끼리 짝지음.
    /// </summary>
    [WorldSystemFilter(WorldSystemFilterFlags.ClientSimulation)]
    [UpdateInGroup(typeof(GhostSpawnClassificationSystemGroup))]
    [UpdateAfter(typeof(GhostSpawnClassificationSystem))]
    [CreateAfter(typeof(GhostCollectionSystem))]
    [CreateAfter(typeof(GhostReceiveSystem))]
    [BurstCompile]
    public partial struct ProjectileSpawnClassificationSystem : ISystem
    {
        // 스냅샷 바이트열에서 특정 컴포넌트를 꺼내오기 위한 헬퍼.
        private SnapshotDataLookupHelper lookupHelper;

        private ComponentLookup<ProjectileSpawnIdComponent> spawnIdLookup;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            lookupHelper = new SnapshotDataLookupHelper(
                ref state,
                SystemAPI.GetSingletonEntity<GhostCollection>(),
                SystemAPI.GetSingletonEntity<SpawnedGhostEntityMap>()
            );

            spawnIdLookup = state.GetComponentLookup<ProjectileSpawnIdComponent>(true);

            state.RequireForUpdate<GhostCollection>();
            state.RequireForUpdate<GhostSpawnQueue>();
            state.RequireForUpdate<PredictedGhostSpawnList>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            lookupHelper.Update(ref state);
            spawnIdLookup.Update(ref state);

            SnapshotDataBufferComponentLookup snapshotLookup = lookupHelper.CreateSnapshotBufferLookup();
            DynamicBuffer<PredictedGhostSpawn> predictedSpawns = SystemAPI.GetSingletonBuffer<PredictedGhostSpawn>();

            foreach (
                var (spawns, snapshots) in
                SystemAPI
                    .Query<DynamicBuffer<GhostSpawnBuffer>, DynamicBuffer<SnapshotDataBuffer>>()
                    .WithAll<GhostSpawnQueue>()
            ) {
                for (int i = 0; i < spawns.Length; i++)
                {
                    ref GhostSpawnBuffer spawn = ref spawns.ElementAt(i);

                    // 예측 대상이 아니거나 이미 처리된 Ghost.
                    if (spawn.SpawnType != GhostSpawnBuffer.Type.Predicted
                        || spawn.HasClassifiedPredictedSpawn
                        || spawn.PredictedSpawnEntity != Entity.Null)
                    {
                        continue;
                    }

                    // 투사체가 아닌 Ghost(우주선 등)는 기본 분류 시스템에 넘김.
                    if (!snapshotLookup.TryGetComponentDataFromSpawnBuffer(
                            spawn, snapshots, out ProjectileSpawnIdComponent received))
                    {
                        continue;
                    }

                    // 여기부터는 투사체. 짝을 못 찾더라도 기본 분류가 엉뚱한 총알을
                    // 집어가지 않도록 처리 완료로 표시함.
                    spawn.HasClassifiedPredictedSpawn = true;

                    for (int j = 0; j < predictedSpawns.Length; j++)
                    {
                        if (spawn.GhostType != predictedSpawns[j].ghostType)
                        {
                            continue;
                        }

                        Entity candidate = predictedSpawns[j].entity;

                        if (!spawnIdLookup.TryGetComponent(candidate, out ProjectileSpawnIdComponent predicted)
                            || predicted.Value != received.Value)
                        {
                            continue;
                        }

                        spawn.PredictedSpawnEntity = candidate;
                        predictedSpawns.RemoveAtSwapBack(j);
                        break;
                    }
                }
            }
        }
    }
}