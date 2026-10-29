using GalaxyIO.DOTS.Collision;
using GalaxyIO.DOTS.Components.Actor;
using GalaxyIO.Tools;
using System.Collections.Generic;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.NetCode;
using Unity.Transforms;
using UnityEngine;

namespace GalaxyIO.Scripts.Managed.UI
{
    /// <summary>
    /// 우주선 선체 아래에 체력바를 그리는 클라이언트 전용 뷰.
    /// </summary>
    public sealed class SpaceshipHealthBars : MonoBehaviour
    {
        [SerializeField]
        [Tooltip("씬에 배치된 체력바 템플릿")]
        private HealthBarView barTemplate;

        [SerializeField]
        [Tooltip("선체 아래쪽 끝과 체력바 사이 간격")]
        private float gap = 0.2F;

        private EntityManager entityManager;
        private EntityQuery spaceshipQuery;

        // 우주선 수만큼 늘어나는 체력바 풀
        private readonly List<HealthBarView> bars = new List<HealthBarView>();

        // 캐시한 쿼리가 속한 클라이언트 월드. 세션 종료 시 Dispose 되므로 매 프레임 확인함.
        private World clientWorld;

        private void Awake()
        {
            barTemplate.gameObject.SetActive(false);
        }

        private void Start()
        {
            clientWorld = ClientServerBootstrap.ClientWorld;

            if (clientWorld == null)
            {
                enabled = false;
                return;
            }

            entityManager = clientWorld.EntityManager;
            spaceshipQuery = entityManager.CreateEntityQuery(
                ComponentType.ReadOnly<SpaceshipSpecComponent>(),
                ComponentType.ReadOnly<ColliderComponent>(),
                ComponentType.ReadOnly<LocalTransform>()
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

            using var spaceships = spaceshipQuery.ToEntityArray(Allocator.Temp);

            int shown = 0;

            foreach (Entity spaceship in spaceships)
            {
                var spec = entityManager.GetComponentData<SpaceshipSpecComponent>(spaceship);

                // 체력이 가득 찬 우주선은 표시하지 않음.
                if (spec.MaxHealth <= math.EPSILON || spec.Health >= spec.MaxHealth)
                {
                    continue;
                }

                var transformData = entityManager.GetComponentData<LocalTransform>(spaceship);

                // 이름표와 같이 회전한 선체의 아래쪽 끝에 붙임.
                Aabb bounds = ColliderBounds.ToAabb(
                    ColliderOverlap.ToWorld(entityManager.GetComponentData<ColliderComponent>(spaceship), transformData)
                );

                HealthBarView bar = Rent(shown++);
                bar.transform.position = new Vector3(
                    transformData.Position.x,
                    bounds.Min.y - gap,
                    transformData.Position.z
                );

                bar.SetRatio(math.saturate(spec.Health / spec.MaxHealth));
            }

            // 이번 프레임에 쓰이지 않은 바는 감춤.
            for (int i = shown; i < bars.Count; i++)
            {
                bars[i].gameObject.SetActive(false);
            }
        }

        private HealthBarView Rent(int index)
        {
            while (bars.Count <= index)
            {
                bars.Add(Instantiate(barTemplate, transform));
            }

            bars[index].gameObject.SetActive(true);
            return bars[index];
        }
    }
}