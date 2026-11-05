using GalaxyIO.DOTS.Components.Actor;
using GalaxyIO.DOTS.Components.Message;
using GalaxyIO.DOTS.Components.Singleton;
using GalaxyIO.DOTS.Networking;
using GalaxyIO.DOTS.Systems;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.NetCode;
using UnityEngine;

namespace GalaxyIO.DOTS.Spectator
{
    /// <summary>
    /// 관찰자 접속 표시. 서버만.
    /// </summary>
    public struct SpectatorTag : IComponentData
    {
    }

    /// <summary>
    /// 관찰자 접속을 받는 서버 시스템. 데디케이티드 서버만.
    /// <para/>
    /// 관찰자 이름(<see cref="SpectatorSettings.Name"/>)으로 온 접속 요청(<see cref="SpawnRequestRPC"/>)은
    /// 우주선을 만들지 않고 Ghost 만 받게 함(<see cref="PlayerSpawnSystem"/>보다 먼저 가로챔).
    /// 자유 모드에서는 우주선 없는 접속이 보내는 카메라 위치(<see cref="SpectateRPC"/>)로 복제 중심을 옮김.
    /// 팀 모드에서는 <see cref="GameModeSystem"/>이 같은 일을 하므로 건드리지 않음.
    /// 기존 RPC 만 쓰므로 일반 클라이언트와 프로토콜이 같음.
    /// </summary>
    [WorldSystemFilter(WorldSystemFilterFlags.ServerSimulation)]
    [UpdateInGroup(typeof(SimulationSystemGroup), OrderFirst = true)]
    [UpdateAfter(typeof(GhostSimulationSystemGroup))]
    [UpdateBefore(typeof(PredictedSimulationSystemGroup))]
    [UpdateBefore(typeof(PlayerSpawnSystem))]
    public partial struct SpectatorServerSystem : ISystem
    {
        private EntityQuery spawnQuery;
        private EntityQuery spectateQuery;
        private bool announced;

        /// <summary>
        /// 관찰자 접속 이름인지. <see cref="PlayerSpawnSystem"/>이 이 요청을 건너뛰고 이 시스템에 남김.
        /// </summary>
        public static bool IsSpectatorName(in FixedString64Bytes name)
        {
            SpectatorSettings settings = DedicatedServerConfig.Current?.Spectator;

            if (settings == null || !settings.Enabled || string.IsNullOrEmpty(settings.Name) || name.IsEmpty)
            {
                return false;
            }

            var spectatorName = new FixedString64Bytes();
            spectatorName.CopyFromTruncated(settings.Name);
            return name == spectatorName;
        }

        public void OnCreate(ref SystemState state)
        {
            spawnQuery = SystemAPI.QueryBuilder().WithAll<ReceiveRpcCommandRequest, SpawnRequestRPC>().Build();
            spectateQuery = SystemAPI.QueryBuilder().WithAll<ReceiveRpcCommandRequest, SpectateRPC>().Build();
            state.RequireAnyForUpdate(spawnQuery, spectateQuery);
        }

        public void OnUpdate(ref SystemState state)
        {
            SpectatorSettings settings = DedicatedServerConfig.Current?.Spectator;

            if (!announced)
            {
                announced = true;
                Debug.Log(settings == null
                    ? "[Spectator] 서버 설정 없음(데디케이티드 서버 아님). 관찰자 접속을 받지 않음."
                    : $"[Spectator] 관찰자 접속 {(settings.Enabled ? "받음" : "꺼짐")}: 이름 {settings.Name}, 반경 {settings.Radius}");
            }

            if (settings == null || !settings.Enabled || string.IsNullOrEmpty(settings.Name))
            {
                return;
            }

            EntityManager entityManager = state.EntityManager;
            var spectatorName = new FixedString64Bytes();
            spectatorName.CopyFromTruncated(settings.Name);

            // 관찰자 접속 요청 가로채기
            if (!spawnQuery.IsEmpty)
            {
                using NativeArray<Entity> rpcs = spawnQuery.ToEntityArray(Allocator.Temp);

                foreach (Entity rpc in rpcs)
                {
                    FixedString64Bytes requested = entityManager.GetComponentData<SpawnRequestRPC>(rpc).Name;

                    if (!requested.IsEmpty)
                    {
                        Debug.Log($"[Spectator] 접속 요청 이름 '{requested}' (관찰자 이름 '{spectatorName}', 일치 {requested == spectatorName})");
                    }

                    if (requested != spectatorName)
                    {
                        continue;
                    }

                    Entity connection = entityManager.GetComponentData<ReceiveRpcCommandRequest>(rpc).SourceConnection;
                    entityManager.DestroyEntity(rpc);

                    if (!entityManager.HasComponent<NetworkId>(connection))
                    {
                        continue;
                    }

                    // Ghost 스냅샷을 받으려면 InGame 이어야 함.
                    if (!entityManager.HasComponent<NetworkStreamInGame>(connection))
                    {
                        entityManager.AddComponent<NetworkStreamInGame>(connection);
                    }

                    if (!entityManager.HasComponent<SpectatorTag>(connection))
                    {
                        entityManager.AddComponent<SpectatorTag>(connection);
                    }

                    entityManager.AddComponentData(connection, new GhostConnectionPosition { Position = float3.zero });
                    entityManager.AddComponentData(connection, new RelevancyRadiusComponent { Value = math.max(settings.Radius, 50.0F) });

                    Debug.Log($"[Spectator] 관찰자 접속 (NetworkId {entityManager.GetComponentData<NetworkId>(connection).Value})");
                }
            }

            // 자유 모드: 우주선 없는 접속의 카메라 위치로 복제 중심을 옮김. 팀 모드는 GameModeSystem 이 처리함.
            if (!spectateQuery.IsEmpty && !SystemAPI.HasSingleton<GameModeComponent>())
            {
                using NativeArray<ReceiveRpcCommandRequest> requests = spectateQuery.ToComponentDataArray<ReceiveRpcCommandRequest>(Allocator.Temp);
                using NativeArray<SpectateRPC> spectates = spectateQuery.ToComponentDataArray<SpectateRPC>(Allocator.Temp);
                entityManager.DestroyEntity(spectateQuery);

                // 우주선이 있으면 GhostRelevancySystem 이 우주선 위치로 다시 덮어씀.
                for (int i = 0; i < requests.Length; i++)
                {
                    Entity connection = requests[i].SourceConnection;

                    if (entityManager.HasComponent<GhostConnectionPosition>(connection))
                    {
                        entityManager.SetComponentData(connection, new GhostConnectionPosition
                        {
                            Position = new float3(spectates[i].Position, 0.0F),
                        });
                    }
                }
            }
        }
    }
}
