using GalaxyIO.DOTS.Client.Components;
using Unity.Entities;
using Unity.NetCode;
using UnityEngine;

namespace GalaxyIO.DOTS.Client.Networking
{
    /// <summary>
    /// 클라이언트에서 서버로 보내는 요청들을 모아놓은 Helper 클래스.
    /// </summary>
    public static class ClientNetworkRequestManager
    {
        /// <summary>
        /// 플레이어 스폰 요청. 의사 표시만 남기고, 실제 접속과 RPC 전송은
        /// <see cref="Systems.ClientConnectionSystem"/>이 처리함.
        /// </summary>
        /// <returns>
        /// 요청 접수 여부. Client World가 없으면 false. 접수됐다는 뜻이지
        /// 접속 완료를 의미하지는 않음.
        /// </returns>
        public static bool RequestPlayerSpawn()
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
                entityManager.AddComponent<PlayerSpawnRequest>(request);
            }

            return true;
        }
    }
}
