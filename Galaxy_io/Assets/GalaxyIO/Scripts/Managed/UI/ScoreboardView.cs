using GalaxyIO.DOTS.Client.Components.Message;
using System.Collections.Generic;
using Unity.Collections;
using Unity.Entities;
using Unity.NetCode;
using UnityEngine;

namespace GalaxyIO.Scripts.Managed.UI
{
    /// <summary>
    /// 화면 좌측 상단에 점수 상위 플레이어를 보여주는 클라이언트 전용 뷰.
    /// </summary>
    public sealed class ScoreboardView : MonoBehaviour
    {
        [SerializeField]
        private ScoreboardRow rowPrefab;

        [SerializeField]
        [Tooltip("줄이 쌓이는 부모. 위에서 아래로 정렬됨.")]
        private RectTransform container;

        [SerializeField]
        [Tooltip("보여줄 상위 플레이어 수")]
        [Min(1)]
        private int maxRows = 5;

        [Header("Colors")]

        [SerializeField]
        private Color nameColor = Color.white;

        [SerializeField]
        [Tooltip("로컬 플레이어 이름 색")]
        private Color localNameColor = new Color(0.95F, 0.3F, 0.3F);

        private struct Entry
        {
            public int NetworkId;
            public string Name;
            public int Score;
        }

        private EntityManager entityManager;
        private EntityQuery messageQuery;
        private EntityQuery connectionQuery;

        // NetworkId 별 점수
        private readonly Dictionary<int, Entry> entries = new Dictionary<int, Entry>();
        private readonly List<Entry> ranking = new List<Entry>();

        private ScoreboardRow[] rows;

        private void Start()
        {
            World clientWorld = ClientServerBootstrap.ClientWorld;

            if (clientWorld == null)
            {
                enabled = false;
                return;
            }

            entityManager = clientWorld.EntityManager;
            messageQuery = entityManager.CreateEntityQuery(ComponentType.ReadOnly<ScoreboardMessage>());
            connectionQuery = entityManager.CreateEntityQuery(ComponentType.ReadOnly<NetworkId>());

            rows = new ScoreboardRow[maxRows];

            for (int i = 0; i < rows.Length; i++)
            {
                rows[i] = Instantiate(rowPrefab, container);
                rows[i].gameObject.SetActive(false);
            }
        }

        private void LateUpdate()
        {
            // 접속이 끊기면 비움. 다시 입장하면 서버가 전체 점수를 다시 보냄.
            if (connectionQuery.IsEmpty)
            {
                entityManager.DestroyEntity(messageQuery);

                if (entries.Count > 0)
                {
                    entries.Clear();
                    Refresh(0);
                }

                return;
            }

            if (messageQuery.IsEmpty)
            {
                return;
            }

            using var messages = messageQuery.ToComponentDataArray<ScoreboardMessage>(Allocator.Temp);

            foreach (ScoreboardMessage message in messages)
            {
                if (message.Left)
                {
                    entries.Remove(message.NetworkId);
                    continue;
                }

                entries[message.NetworkId] = new Entry
                {
                    NetworkId = message.NetworkId,
                    Name      = message.Name.ToString(),
                    Score     = message.Score,
                };
            }

            entityManager.DestroyEntity(messageQuery);

            Refresh(connectionQuery.GetSingleton<NetworkId>().Value);
        }

        private void Refresh(int localNetworkId)
        {
            ranking.Clear();
            ranking.AddRange(entries.Values);
            ranking.Sort(Compare);

            for (int i = 0; i < rows.Length; i++)
            {
                bool visible = i < ranking.Count;
                rows[i].gameObject.SetActive(visible);

                if (visible)
                {
                    Entry entry = ranking[i];
                    Color color = entry.NetworkId == localNetworkId ? localNameColor : nameColor;

                    rows[i].Set(i + 1, entry.Name, entry.Score, color);
                }
            }
        }

        // 점수 내림차순. 같으면 NetworkId 오름차순.
        private static int Compare(Entry a, Entry b)
        {
            int order = b.Score.CompareTo(a.Score);
            return order != 0 ? order : a.NetworkId.CompareTo(b.NetworkId);
        }
    }
}
