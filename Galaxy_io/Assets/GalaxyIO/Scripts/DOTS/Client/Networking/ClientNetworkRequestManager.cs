using GalaxyIO.DOTS.Components;
using Unity.Entities;
using Unity.NetCode;
using UnityEngine;

namespace GalaxyIO.DOTS.Client.Networking
{
    /// <summary>
    /// 클라이언트에서 서버로 보내는 요청들을 모아놓은 Helper클래스
    /// </summary>
    public static class ClientNetworkRequestManager
    {
        /// <summary>
        /// 플레이어 스폰 요청
        /// </summary>
        public static void RequestPlayerSpawn()
        {
            // Thin Client 를 제외한 실제 Client World.
            var clientWorld = ClientServerBootstrap.ClientWorld;

            if (clientWorld == null)
            {
                Debug.LogWarning("Client World not found.");
                return;
            }

            var entityManager = clientWorld.EntityManager;
            using var connectionQuery = entityManager.CreateEntityQuery(ComponentType.ReadOnly<NetworkStreamConnection>());

            if (connectionQuery.IsEmpty)
            {
                Debug.LogWarning("Not connected to server.");
                return;
            }

            var connectionEntity = connectionQuery.GetSingletonEntity();

            // 서버가 보내는 Ghost 스냅샷을 받으려면 클라이언트 Connection 도 InGame 이어야 한다.
            if (!entityManager.HasComponent<NetworkStreamInGame>(connectionEntity))
            {
                entityManager.AddComponent<NetworkStreamInGame>(connectionEntity);
            }

            var rpcEntity = entityManager.CreateEntity();

            entityManager.AddComponentData(
                rpcEntity,
                new SpawnRequestRPC()
            );

            entityManager.AddComponentData(
                rpcEntity,
                new SendRpcCommandRequest {
                    TargetConnection = connectionEntity
                }
            );
        }
    }
}