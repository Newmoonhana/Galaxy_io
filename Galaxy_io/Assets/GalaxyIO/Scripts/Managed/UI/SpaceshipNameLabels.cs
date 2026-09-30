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
    /// </summary>
    public sealed class SpaceshipNameLabels : MonoBehaviour
    {
        [SerializeField]
        [Tooltip("이름표 프리팹. 위쪽 정렬이라 위치 아래로 글자가 붙음.")]
        private TMP_Text labelPrefab;

        [SerializeField]
        [Tooltip("우주선 중심 기준 이름표 위치. 체력바 아래에 둠.")]
        private float offsetY = -0.85F;

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
                var playerName = entityManager.GetComponentData<PlayerNameComponent>(spaceship).Value;

                // 스폰 직후 이름이 아직 도착하지 않았음.
                if (playerName.IsEmpty)
                {
                    continue;
                }

                var transformData = entityManager.GetComponentData<LocalTransform>(spaceship);

                int index = shown++;
                TMP_Text label = Rent(index);

                label.transform.position = new Vector3(
                    transformData.Position.x,
                    transformData.Position.y + offsetY,
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