using GalaxyIO.DOTS.Client.Components;
using Unity.Entities;
using Unity.NetCode;
using Unity.Networking.Transport;
using UnityEngine;

namespace GalaxyIO.DOTS.Client.Networking
{
    /// <summary>
    /// 클라이언트에서 서버로 보내는 요청들을 모아놓은 Helper 클래스.
    /// </summary>
    public static class ClientNetworkRequestManager
    {
        /// <summary>
        /// 지정한 주소의 서버에 접속해 플레이어 스폰을 요청함. 의사 표시만 남기고,
        /// 실제 접속과 RPC 전송은 <see cref="Systems.ClientConnectionSystem"/>이 처리함.
        /// </summary>
        /// <returns>
        /// 요청 접수 여부. Client World가 없으면 false. 접수됐다는 뜻이지
        /// 접속 완료를 의미하지는 않음.
        /// </returns>
        public static bool RequestPlayerSpawn(NetworkEndpoint endpoint)
        {
            return Enqueue(endpoint);
        }

        /// <summary>
        /// 파괴된 뒤 다시 스폰을 요청함. 이미 접속돼 있으므로 주소를 싣지 않음.
        /// </summary>
        public static bool RequestRespawn()
        {
            var clientWorld = ClientServerBootstrap.ClientWorld;

            if (clientWorld == null || !Enqueue(default))
            {
                return false;
            }

            // 태그가 사라지면 리스폰 창이 닫힘.
            using var deathQuery = clientWorld.EntityManager.CreateEntityQuery(
                ComponentType.ReadOnly<PlayerDeathTag>()
            );

            clientWorld.EntityManager.DestroyEntity(deathQuery);

            return true;
        }

        /// <summary>
        /// 요청 엔티티를 남김.
        /// </summary>
        private static bool Enqueue(NetworkEndpoint endpoint)
        {
            // Thin Client 를 제외한 실제 Client World.
            var clientWorld = ClientServerBootstrap.ClientWorld;

            if (clientWorld == null)
            {
                Debug.LogWarning("Client World not found.");
                return false;
            }

            var entityManager = clientWorld.EntityManager;

            using var requestQuery = entityManager.CreateEntityQuery(
                ComponentType.ReadOnly<PlayerSpawnRequest>()
            );

            // 대기 중인 요청이 있으면 중복 생성하지 않음.
            if (requestQuery.IsEmpty)
            {
                Entity request = entityManager.CreateEntity();

                entityManager.AddComponentData(request, new PlayerSpawnRequest
                {
                    Endpoint = endpoint,
                });
            }

            return true;
        }
    }
}
