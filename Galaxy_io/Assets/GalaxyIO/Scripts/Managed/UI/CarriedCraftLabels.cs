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
    /// 교전 중인 자기 함재기 아래에 단계(접근 / 포위)를 그리는 클라이언트 전용 뷰.
    /// </summary>
    public sealed class CarriedCraftLabels : MonoBehaviour
    {
        [SerializeField]
        [Tooltip("단계 표시 프리팹. 위쪽 정렬이라 위치 아래로 글자가 붙음.")]
        private TMP_Text labelPrefab;

        [SerializeField]
        [Tooltip("선체 아래쪽 끝과 글자 사이 간격. 체력바 아래에 둠.")]
        private float gap = 0.35F;

        [SerializeField]
        [Tooltip("글자 색. 명령 표시와 같은 색.")]
        private Color color = new Color(1.0F, 0.75F, 0.3F);

        private EntityManager entityManager;
        private EntityQuery craftQuery;
        private EntityQuery connectionQuery;

        // 함재기 수만큼 늘어나는 표시 풀
        private readonly List<TMP_Text> labels = new List<TMP_Text>();

        // 표시마다 보이는 단계. 바뀔 때만 글자를 다시 씀.
        private readonly List<bool> shownApproaching = new List<bool>();

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
            craftQuery = entityManager.CreateEntityQuery(
                ComponentType.ReadOnly<CarriedCraftComponent>(),
                ComponentType.ReadOnly<ColliderComponent>(),
                ComponentType.ReadOnly<LocalTransform>()
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

            int shown = 0;

            if (connectionQuery.TryGetSingleton(out NetworkId networkId))
            {
                using var crafts = craftQuery.ToEntityArray(Allocator.Temp);

                foreach (Entity craft in crafts)
                {
                    CarriedCraftComponent state = entityManager.GetComponentData<CarriedCraftComponent>(craft);

                    if (state.OwnerNetworkId != networkId.Value || !state.Engaging || state.Returning)
                    {
                        continue;
                    }

                    var transformData = entityManager.GetComponentData<LocalTransform>(craft);
                    Aabb bounds = ColliderBounds.ToAabb(
                        ColliderOverlap.ToWorld(entityManager.GetComponentData<ColliderComponent>(craft), transformData)
                    );

                    int index = shown++;
                    TMP_Text label = Rent(index, state.Approaching);

                    label.transform.position = new Vector3(transformData.Position.x, bounds.Min.y - gap, transformData.Position.z);

                    if (shownApproaching[index] != state.Approaching)
                    {
                        shownApproaching[index] = state.Approaching;
                        label.text = L10n.Get(state.Approaching ? "Order/Approaching" : "Order/Surrounding");
                    }
                }
            }

            // 이번 프레임에 쓰이지 않은 표시는 감춤.
            for (int i = shown; i < labels.Count; i++)
            {
                labels[i].gameObject.SetActive(false);
            }
        }

        // 새로 만든 표시는 글자가 바로 채워지도록 반대 단계로 둠.
        private TMP_Text Rent(int index, bool approaching)
        {
            while (labels.Count <= index)
            {
                TMP_Text label = Instantiate(labelPrefab, transform);
                label.color = color;

                labels.Add(label);
                shownApproaching.Add(!approaching);
            }

            labels[index].gameObject.SetActive(true);
            return labels[index];
        }
    }
}
