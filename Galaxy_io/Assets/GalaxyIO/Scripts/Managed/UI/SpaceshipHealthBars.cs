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
    /// 우주선 아래에 체력바를 그리는 클라이언트 전용 뷰.
    /// </summary>
    public sealed class SpaceshipHealthBars : MonoBehaviour
    {
        [SerializeField]
        [Tooltip("체력바 프리팹")]
        private HealthBarView barPrefab;

        [SerializeField]
        [Tooltip("우주선 중심 기준 체력바 위치")]
        private float offsetY = -0.7F;

        private EntityManager entityManager;
        private EntityQuery spaceshipQuery;

        // 우주선 수만큼 늘어나는 체력바 풀
        private readonly List<HealthBarView> bars = new List<HealthBarView>();

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
            spaceshipQuery = entityManager.CreateEntityQuery(
                ComponentType.ReadOnly<SpaceshipSpecComponent>(),
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

                HealthBarView bar = Rent(shown++);
                bar.transform.position = new Vector3(
                    transformData.Position.x,
                    transformData.Position.y + offsetY,
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
                bars.Add(Instantiate(barPrefab, transform));
            }

            bars[index].gameObject.SetActive(true);
            return bars[index];
        }
    }
}