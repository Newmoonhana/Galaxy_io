using GalaxyIO.DOTS.Collision;
using GalaxyIO.DOTS.Components.Actor;
using GalaxyIO.Tools;
using System.Collections.Generic;
using TMPro;
using Unity.Collections;
using Unity.Entities;
using Unity.NetCode;
using Unity.Transforms;
using UnityEngine;

namespace GalaxyIO.Scripts.Managed.UI
{
    /// <summary>
    /// 플레이어 우주선 아래에 이름을 그리는 클라이언트 전용 뷰.
    /// 회전한 선체 충돌체의 아래쪽 끝에 붙이고, 선체 두께(Thickness)만큼 크게 그림.
    /// </summary>
    public sealed class SpaceshipNameLabels : MonoBehaviour
    {
        [SerializeField]
        [Tooltip("이름표 프리팹. 위쪽 정렬이라 위치 아래로 글자가 붙음.")]
        private TMP_Text labelPrefab;

        [SerializeField]
        [Tooltip("선체 아래쪽 끝과 이름표 사이 간격. 체력바 아래에 둠. 두께 배율이 함께 곱해짐.")]
        private float gap = 0.35F;

        [SerializeField]
        [Tooltip("두께 1 당 늘어나는 크기 배율. 두께 1이면 프리팹 크기 그대로.")]
        [Min(0.0F)]
        private float scalePerThickness = 0.1F;

        // 프리팹 크기. 두께 배율의 기준.
        private Vector3 baseScale;

        private EntityManager entityManager;
        private EntityQuery spaceshipQuery;

        // 우주선 수만큼 늘어나는 이름표 풀
        private readonly List<TMP_Text> labels = new List<TMP_Text>();

        // 이름표마다 표시 중인 이름. 바뀔 때만 문자열을 만들어 GC 할당을 막음.
        private readonly List<FixedString64Bytes> shownNames = new List<FixedString64Bytes>();

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
                ComponentType.ReadOnly<PlayerNameComponent>(),
                ComponentType.ReadOnly<SpaceshipSpecComponent>(),
                ComponentType.ReadOnly<ColliderComponent>(),
                ComponentType.ReadOnly<LocalTransform>()
            );
            baseScale = labelPrefab.transform.localScale;
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
                var playerName = entityManager.GetComponentData<PlayerNameComponent>(spaceship).Value;

                // 스폰 직후 이름이 아직 도착하지 않았음.
                if (playerName.IsEmpty)
                {
                    continue;
                }

                var transformData = entityManager.GetComponentData<LocalTransform>(spaceship);
                float thickness = entityManager.GetComponentData<SpaceshipSpecComponent>(spaceship).Thickness;
                Aabb bounds = ColliderBounds.ToAabb(
                    ColliderOverlap.ToWorld(entityManager.GetComponentData<ColliderComponent>(spaceship), transformData)
                );
                float scale = 1.0F + Mathf.Max(thickness - 1.0F, 0.0F) * scalePerThickness;

                int index = shown++;
                TMP_Text label = Rent(index);

                label.transform.localScale = baseScale * scale;
                label.transform.position = new Vector3(
                    transformData.Position.x,
                    bounds.Min.y - gap * scale,
                    transformData.Position.z
                );

                if (shownNames[index] != playerName)
                {
                    shownNames[index] = playerName;
                    label.text = playerName.ToString();
                }
            }

            // 이번 프레임에 쓰이지 않은 이름표는 감춤.
            for (int i = shown; i < labels.Count; i++)
            {
                labels[i].gameObject.SetActive(false);
            }
        }

        private TMP_Text Rent(int index)
        {
            while (labels.Count <= index)
            {
                labels.Add(Instantiate(labelPrefab, transform));
                shownNames.Add(default);
            }

            labels[index].gameObject.SetActive(true);
            return labels[index];
        }
    }
}