using GalaxyIO.DOTS.Client.Components;
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
        /// 플레이어 스폰 요청.
        /// <p/>
        /// 여기서는 의사 표시만 남기고, 실제 접속과 RPC 전송은
        /// <see cref="Systems.ClientConnectionSystem"/>이 처리한다.
        /// 서버가 아직 떠 있지 않아도 접속될 때까지 재시도하므로
        /// 버튼을 여러 번 누를 필요는 없다.
        /// </summary>
        /// <returns>
        /// 요청이 접수되었으면 true. Client World 자체가 없어서 접수할 수 없으면 false.
        /// <p/>
        /// 주의: true 는 "접수됨"이지 "접속 완료"가 아니다. 접속은 비동기로 진행된다.
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

            // 이미 처리 대기 중인 요청이 있으면 중복 생성하지 않는다.
            if (requestQuery.IsEmpty)
            {
                Entity request = entityManager.CreateEntity();
                entityManager.AddComponent<PlayerSpawnRequest>(request);
            }

            return true;
        }
    }
}
