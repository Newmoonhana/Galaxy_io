using GalaxyIO.DOTS.Components.Actor;
using GalaxyIO.Tools;
using System.Collections.Generic;
using Unity.Collections;
using Unity.Entities;
using Unity.NetCode;
using UnityEngine;

namespace GalaxyIO.Scripts.Managed.UI
{
    /// <summary>
    /// 로컬 우주선이 다른 우주선과 부딪히지 않는 동안 반투명하게 그리는 클라이언트 전용 뷰.
    /// </summary>
    public sealed class LocalSpaceshipPhaseView : MonoBehaviour
    {
        [SerializeField]
        [Tooltip("통과 상태일 때의 알파")]
        [Range(0.0F, 1.0F)]
        private float phaseAlpha = 0.35F;

        [SerializeField]
        [Tooltip("알파가 바뀌는 속도(초당)")]
        [Min(0.01F)]
        private float fadeSpeed = 1.5F;

        private EntityManager entityManager;
        private EntityQuery playerQuery;

        private float alpha = 1.0F;

        // 매 프레임 다시 채움.
        private readonly List<SpriteRenderer> sprites = new List<SpriteRenderer>();

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
                ComponentType.ReadOnly<PlayerSpaceshipMovementComponent>(),
                ComponentType.ReadOnly<MovementComponent>(),
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
                alpha = 1.0F;
                return;
            }

            using var players = playerQuery.ToEntityArray(Allocator.Temp);
            bool phasing = entityManager.GetComponentData<PlayerSpaceshipMovementComponent>(players[0])
                .IsPhasing(entityManager.GetComponentData<MovementComponent>(players[0]).Velocity);

            alpha = Mathf.MoveTowards(alpha, phasing ? phaseAlpha : 1.0F, fadeSpeed * Time.deltaTime);

            // Companion 스프라이트. 리스폰하면 새 인스턴스가 생기므로 매 프레임 칠함.
            SpaceshipSprites.Collect(entityManager, players[0], sprites);

            foreach (SpriteRenderer sprite in sprites)
            {
                Color color = sprite.color;
                color.a = alpha;
                sprite.color = color;
            }
        }
    }
}