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
    /// 로컬 플레이어와 같은 파티원 우주선의 스프라이트에 외곽선 머티리얼을 입히는 클라이언트 전용 뷰.
    /// 로컬 우주선은 <see cref="LocalSpaceshipOutline"/>이 맡음.
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

        // 외곽선을 입힌 스프라이트 -> 원래 머티리얼. 파티를 나가면 되돌림.
        private readonly Dictionary<SpriteRenderer, Material> originals = new Dictionary<SpriteRenderer, Material>();

        // 파괴된 스프라이트. originals 에서 지울 목록.
        private readonly List<SpriteRenderer> destroyed = new List<SpriteRenderer>();

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

            // Companion 스프라이트. 리스폰하면 새 인스턴스가 생기므로 매 프레임 확인함.
            foreach (Entity spaceship in spaceships)
            {
                var sprite = entityManager.GetComponentObject<SpriteRenderer>(spaceship);
                bool outlined = roster.Members.Contains(entityManager.GetComponentData<GhostOwner>(spaceship).NetworkId);

                if (outlined && sprite.sharedMaterial != outlineMaterial)
                {
                    originals[sprite] = sprite.sharedMaterial;
                    sprite.sharedMaterial = outlineMaterial;
                }
                else if (!outlined && originals.Remove(sprite, out Material original))
                {
                    sprite.sharedMaterial = original;
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
    }
}
