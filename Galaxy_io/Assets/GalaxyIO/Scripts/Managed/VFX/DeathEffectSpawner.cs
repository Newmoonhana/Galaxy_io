using System.Collections.Generic;
using GalaxyIO.DOTS.Client.Components;
using Unity.Collections;
using Unity.Entities;
using Unity.NetCode;
using UnityEngine;

namespace GalaxyIO.Scripts.Managed.VFX
{
    /// <summary>
    /// 우주선이 파괴된 자리에 폭발 연출을 띄우는 클라이언트 전용 스포너.
    /// </summary>
    public sealed class DeathEffectSpawner : MonoBehaviour
    {
        [SerializeField]
        [Tooltip("폭발 연출 프리팹")]
        private ParticleSystem effectPrefab;

        private EntityManager entityManager;
        private EntityQuery eventQuery;

        // 동시에 터지는 수만큼 늘어나는 연출 풀
        private readonly List<ParticleSystem> effects = new List<ParticleSystem>();

        private void Start()
        {
            World clientWorld = ClientServerBootstrap.ClientWorld;

            if (clientWorld == null)
            {
                enabled = false;
                return;
            }

            entityManager = clientWorld.EntityManager;
            eventQuery = entityManager.CreateEntityQuery(
                ComponentType.ReadOnly<DeathEffectEvent>()
            );
        }

        private void Update()
        {
            if (eventQuery.IsEmpty)
            {
                return;
            }

            using var events = eventQuery.ToComponentDataArray<DeathEffectEvent>(Allocator.Temp);

            foreach (DeathEffectEvent deathEffect in events)
            {
                ParticleSystem effect = Rent();

                effect.transform.position = new Vector3(deathEffect.Position.x, deathEffect.Position.y, 0.0F);
                effect.gameObject.SetActive(true);
                effect.Play(true);
            }

            entityManager.DestroyEntity(eventQuery);
        }

        /// <summary>
        /// 재생이 끝나 꺼져 있는 연출을 재사용하고, 없으면 새로 만듦.
        /// 다 쓴 연출은 Stop Action 이 스스로 꺼 줌.
        /// </summary>
        private ParticleSystem Rent()
        {
            foreach (ParticleSystem effect in effects)
            {
                if (!effect.gameObject.activeSelf)
                {
                    return effect;
                }
            }

            ParticleSystem created = Instantiate(effectPrefab, transform);

            created.gameObject.SetActive(false);
            effects.Add(created);

            return created;
        }
    }
}
