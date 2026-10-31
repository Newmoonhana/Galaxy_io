using GalaxyIO.DOTS.Components.Message;
using GalaxyIO.DOTS.Components.Singleton;
using GalaxyIO.Tools;
using System.Collections.Generic;
using Unity.Collections;
using Unity.Entities;
using Unity.NetCode;
using UnityEngine;

namespace GalaxyIO.Scripts.Managed.UI
{
    /// <summary>
    /// 화면 좌측 상단에 점수 상위 플레이어를 보여주는 클라이언트 전용 뷰. 팀 모드에서는 처치 수 상위 플레이어를 보여 줌.
    /// </summary>
    public sealed class ScoreboardView : MonoBehaviour
    {
        [SerializeField]
        [Tooltip("씬에 배치된 순위 한 줄 템플릿")]
        private ScoreboardRow rowTemplate;

        [SerializeField]
        [Tooltip("줄이 쌓이는 부모. 위에서 아래로 정렬됨.")]
        private RectTransform container;

        [SerializeField]
        [Tooltip("보여줄 상위 플레이어 수")]
        [Min(1)]
        private int maxRows = 5;

        [SerializeField]
        [Tooltip("로컬 플레이어가 얻은 점수를 띄울 뷰")]
        private DamagePopupView popups;

        [SerializeField]
        [Tooltip("팀 모드에서 처치 수를 받아 오는 팀 점수판")]
        private TeamScoreboardView teamScoreboard;

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
        private EntityQuery modeQuery;

        // 마지막으로 반영한 팀 점수판 버전
        private int teamVersion = -1;

        // NetworkId 별 점수
        private readonly Dictionary<int, Entry> entries = new Dictionary<int, Entry>();
        private readonly List<Entry> ranking = new List<Entry>();

        private ScoreboardRow[] rows;

        // 캐시한 쿼리가 속한 클라이언트 월드. 세션 종료 시 Dispose 되므로 매 프레임 확인함.
        private World clientWorld;

        private void Awake()
        {
            rowTemplate.gameObject.SetActive(false);
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
            messageQuery = entityManager.CreateEntityQuery(
                ComponentType.ReadOnly<PlayerScoreRPC>(),
                ComponentType.ReadOnly<ReceiveRpcCommandRequest>()
            );
            connectionQuery = entityManager.CreateEntityQuery(ComponentType.ReadOnly<NetworkId>());
            modeQuery = entityManager.CreateEntityQuery(ComponentType.ReadOnly<GameModeComponent>());

            rows = new ScoreboardRow[maxRows];

            for (int i = 0; i < rows.Length; i++)
            {
                rows[i] = Instantiate(rowTemplate, container);
                rows[i].gameObject.SetActive(false);
            }
        }

        private void LateUpdate()
        {
            // 세션을 나가면 클라이언트 월드가 Dispose 됨. 캐시한 쿼리를 더 쓰면 안 됨.
            if (!ClientWorldScope.IsAlive(clientWorld))
            {
                enabled = false;
                return;
            }

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

            // 팀 모드는 점수 대신 팀 점수판의 처치 수로 줄을 세움.
            if (!modeQuery.IsEmpty)
            {
                entityManager.DestroyEntity(messageQuery);

                if (teamVersion == teamScoreboard.Version)
                {
                    return;
                }

                teamVersion = teamScoreboard.Version;
                entries.Clear();

                foreach (TeamScoreboardView.Entry stat in teamScoreboard.Entries)
                {
                    entries[stat.NetworkId] = new Entry { NetworkId = stat.NetworkId, Name = stat.Name, Score = stat.Kills };
                }

                Refresh(connectionQuery.GetSingleton<NetworkId>().Value);
                return;
            }

            if (messageQuery.IsEmpty)
            {
                return;
            }

            using var messages = messageQuery.ToComponentDataArray<PlayerScoreRPC>(Allocator.Temp);

            int localNetworkId = connectionQuery.GetSingleton<NetworkId>().Value;

            foreach (PlayerScoreRPC message in messages)
            {
                if (message.Left)
                {
                    entries.Remove(message.NetworkId);
                    continue;
                }

                // 로컬 플레이어 점수가 오르면 오른 만큼 띄움. 사망으로 0 이 되는 것은 띄우지 않음.
                if (message.NetworkId == localNetworkId
                    && entries.TryGetValue(message.NetworkId, out Entry before)
                    && message.Score > before.Score
                    && popups != null)
                {
                    popups.ShowScore(message.Score - before.Score);
                }

                entries[message.NetworkId] = new Entry
                {
                    NetworkId = message.NetworkId,
                    Name      = message.Name.ToString(),
                    Score     = message.Score,
                };
            }

            entityManager.DestroyEntity(messageQuery);

            Refresh(localNetworkId);
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
