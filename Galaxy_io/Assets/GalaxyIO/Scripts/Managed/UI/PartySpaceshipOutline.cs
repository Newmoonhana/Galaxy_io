using GalaxyIO.DOTS.Components.Actor;
using GalaxyIO.DOTS.Components.Message;
using GalaxyIO.Tools;
using System.Collections.Generic;
using Unity.Collections;
using Unity.Entities;
using Unity.NetCode;
using UnityEngine;

namespace GalaxyIO.Scripts.Managed.UI
{
    /// <summary>
    /// 로컬 플레이어와 같은 파티원 우주선과 그 함재기의 스프라이트(선체, 자동 포탑)에 외곽선 머티리얼을 입히는 클라이언트 전용 뷰.
    /// 로컬 우주선과 그 함재기는 <see cref="LocalSpaceshipOutline"/>이 맡음.
    /// </summary>
    public sealed class PartySpaceshipOutline : MonoBehaviour
    {
        [SerializeField]
        [Tooltip("파티원 외곽선 머티리얼")]
        private Material outlineMaterial;

        [SerializeField]
        [Tooltip("파티 구성을 받는 채팅창")]
        private ChatView chat;

        private EntityManager entityManager;
        private EntityQuery spaceshipQuery;
        private EntityQuery craftQuery;
        private EntityQuery connectionQuery;

        // 외곽선을 입힌 스프라이트 -> 원래 머티리얼. 파티를 나가면 되돌림.
        private readonly Dictionary<SpriteRenderer, Material> originals = new Dictionary<SpriteRenderer, Material>();

        // 파괴된 스프라이트. originals 에서 지울 목록.
        private readonly List<SpriteRenderer> destroyed = new List<SpriteRenderer>();

        // 우주선 하나의 스프라이트. 우주선마다 다시 채움.
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

            // 로컬 우주선은 제외.
            spaceshipQuery = entityManager.CreateEntityQuery(new EntityQueryDesc
            {
                All = new[]
                {
                    ComponentType.ReadOnly<PlayerNameComponent>(),
                    ComponentType.ReadOnly<GhostOwner>(),
                    ComponentType.ReadOnly<SpriteRenderer>(),
                },
                None = new[] { ComponentType.ReadOnly<GhostOwnerIsLocal>() },
            });
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

            PartyRosterRPC roster = chat.PartyRoster;
            using var spaceships = spaceshipQuery.ToEntityArray(Allocator.Temp);

            // Companion 스프라이트. 리스폰, 출격하면 새 인스턴스가 생기므로 매 프레임 확인함.
            foreach (Entity spaceship in spaceships)
            {
                Outline(spaceship, roster.Members.Contains(entityManager.GetComponentData<GhostOwner>(spaceship).NetworkId));
            }

            // 함재기는 주인 기준. 자기 함재기는 제외.
            if (connectionQuery.TryGetSingleton(out NetworkId networkId))
            {
                using var crafts = craftQuery.ToEntityArray(Allocator.Temp);

                foreach (Entity craft in crafts)
                {
                    int owner = entityManager.GetComponentData<CarriedCraftComponent>(craft).OwnerNetworkId;

                    if (owner != networkId.Value)
                    {
                        Outline(craft, roster.Members.Contains(owner));
                    }
                }
            }

            foreach (SpriteRenderer sprite in originals.Keys)
            {
                if (sprite == null)
                {
                    destroyed.Add(sprite);
                }
            }

            foreach (SpriteRenderer sprite in destroyed)
            {
                originals.Remove(sprite);
            }

            destroyed.Clear();
        }

        // 파티원이면 외곽선을 입히고, 아니면 입혔던 것을 되돌림.
        private void Outline(Entity spaceship, bool outlined)
        {
            SpaceshipSprites.Collect(entityManager, spaceship, sprites);

            foreach (SpriteRenderer sprite in sprites)
            {
                if (outlined)
                {
                    Material outline = OutlineMaterials.WithEmission(outlineMaterial, sprite.sharedMaterial);

                    if (sprite.sharedMaterial != outline)
                    {
                        originals[sprite] = sprite.sharedMaterial;
                        sprite.sharedMaterial = outline;
                    }
                }
                else if (originals.Remove(sprite, out Material original))
                {
                    sprite.sharedMaterial = original;
                }
            }
        }
    }
}
