using GalaxyIO.DOTS.Components.Actor;
using GalaxyIO.Tools;
using Unity.Collections;
using Unity.Entities;
using Unity.NetCode;
using UnityEngine;

namespace GalaxyIO.Scripts.Managed.UI
{
    /// <summary>
    /// 로컬 플레이어 우주선의 스프라이트에 외곽선 머티리얼을 입히는 클라이언트 전용 뷰.
    /// </summary>
    public sealed class LocalSpaceshipOutline : MonoBehaviour
    {
        [SerializeField]
        [Tooltip("외곽선 머티리얼")]
        private Material outlineMaterial;

        private EntityManager entityManager;
        private EntityQuery playerQuery;

        // 캐시한 쿼리가 속한 클라이언트 월드. 세션 종료 시 Dispose 되므로 매 프레임 확인함.
        private World clientWorld;

        private void Start()
        {
            clientWorld = ClientServerBootstrap.ClientWorld;

            if (clientWorld == null)
            {
                enabled = false;
                return;
            }

            entityManager = clientWorld.EntityManager;
            playerQuery = entityManager.CreateEntityQuery(
                ComponentType.ReadOnly<GhostOwnerIsLocal>(),
                ComponentType.ReadOnly<SpaceshipSpecComponent>(),
                ComponentType.ReadOnly<SpriteRenderer>()
            );
        }

        private void LateUpdate()
        {
            // 세션을 나가면 클라이언트 월드가 Dispose 됨. 캐시한 쿼리를 더 쓰면 안 됨.
            if (!ClientWorldScope.IsAlive(clientWorld))
            {
                enabled = false;
                return;
            }

            if (playerQuery.IsEmpty)
            {
                return;
            }

            using var players = playerQuery.ToEntityArray(Allocator.Temp);

            // Companion 스프라이트. 리스폰하면 새 인스턴스가 생기므로 매 프레임 확인함.
            var sprite = entityManager.GetComponentObject<SpriteRenderer>(players[0]);

            if (sprite.sharedMaterial != outlineMaterial)
            {
                sprite.sharedMaterial = outlineMaterial;
            }
        }
    }
}