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
    /// 로컬 플레이어 우주선과 그 함재기의 스프라이트(선체, 자동 포탑)에 외곽선 머티리얼을 입히는 클라이언트 전용 뷰.
    /// </summary>
    public sealed class LocalSpaceshipOutline : MonoBehaviour
    {
        [SerializeField]
        [Tooltip("외곽선 머티리얼")]
        private Material outlineMaterial;

        private EntityManager entityManager;
        private EntityQuery playerQuery;
        private EntityQuery craftQuery;
        private EntityQuery connectionQuery;

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
                ComponentType.ReadOnly<SpaceshipSpecComponent>(),
                ComponentType.ReadOnly<SpriteRenderer>()
            );
            craftQuery = entityManager.CreateEntityQuery(
                ComponentType.ReadOnly<CarriedCraftComponent>(),
                ComponentType.ReadOnly<SpriteRenderer>()
            );
            connectionQuery = entityManager.CreateEntityQuery(ComponentType.ReadOnly<NetworkId>());
        }

        private void LateUpdate()
        {
            // 세션을 나가면 클라이언트 월드가 Dispose 됨. 캐시한 쿼리를 더 쓰면 안 됨.
            if (!ClientWorldScope.IsAlive(clientWorld))
            {
                enabled = false;
                return;
            }

            // Companion 스프라이트. 리스폰, 출격하면 새 인스턴스가 생기므로 매 프레임 확인함.
            using var players = playerQuery.ToEntityArray(Allocator.Temp);

            foreach (Entity player in players)
            {
                Outline(player);
            }

            // 함재기는 주인이 바뀌지 않으므로 되돌릴 일이 없음.
            if (!connectionQuery.TryGetSingleton(out NetworkId networkId))
            {
                return;
            }

            using var crafts = craftQuery.ToEntityArray(Allocator.Temp);

            foreach (Entity craft in crafts)
            {
                if (entityManager.GetComponentData<CarriedCraftComponent>(craft).OwnerNetworkId == networkId.Value)
                {
                    Outline(craft);
                }
            }
        }

        private void Outline(Entity spaceship)
        {
            SpaceshipSprites.Collect(entityManager, spaceship, sprites);

            foreach (SpriteRenderer sprite in sprites)
            {
                if (sprite.sharedMaterial != outlineMaterial)
                {
                    sprite.sharedMaterial = outlineMaterial;
                }
            }
        }
    }
}