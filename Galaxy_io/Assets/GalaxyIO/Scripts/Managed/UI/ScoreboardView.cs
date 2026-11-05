using GalaxyIO.DOTS.Components.Message;
using GalaxyIO.DOTS.Components.Singleton;
using GalaxyIO.Tools;
using System.Collections.Generic;
using DG.Tweening;
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
        [Tooltip("줄이 놓이는 부모. 위쪽 기준으로 코드가 자리를 잡음.")]
        private RectTransform container;

        [SerializeField]
        [Tooltip("줄 수에 맞춰 높이가 바뀌는 배경. 위쪽 기준(pivot y 1).")]
        private RectTransform background;

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

        [Header("Layout")]

        [SerializeField]
        [Tooltip("배경 안쪽 위아래 여백")]
        [Min(0.0F)]
        private float padding = 6.0F;

        [SerializeField]
        [Tooltip("줄 높이")]
        [Min(0.0F)]
        private float rowHeight = 16.0F;

        [SerializeField]
        [Tooltip("줄 사이 간격")]
        [Min(0.0F)]
        private float spacing = 2.0F;

        [SerializeField]
        [Tooltip("순위가 바뀐 줄이 새 자리로 가는 시간(초)")]
        [Min(0.0F)]
        private float moveTime = 0.25F;

        [SerializeField]
        [Tooltip("줄이 나타나거나 사라지는 시간(초)")]
        [Min(0.0F)]
        private float fadeTime = 0.2F;

        [Header("Colors")]

        [SerializeField]
        private Color nameColor = Color.white;

        [SerializeField]
        [Tooltip("로컬 플레이어 이름 색")]
        private Color localNameColor = new Color(1.0F, 0.85F, 0.35F);

        [SerializeField]
        [Tooltip("1위부터의 순위 숫자 색. 넘는 순위는 nameColor.")]
        private Color[] rankColors =
        {
            new Color(1.0F, 0.82F, 0.3F),
            new Color(0.8F, 0.85F, 0.9F),
            new Color(0.85F, 0.55F, 0.35F),
        };

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

        // NetworkId 별로 표시 중인 줄
        private readonly Dictionary<int, ScoreboardRow> shown = new Dictionary<int, ScoreboardRow>();

        // 다시 쓸 줄. 사라지는 중인 줄은 페이드가 끝나야 들어옴.
        private readonly Stack<ScoreboardRow> pool = new Stack<ScoreboardRow>();

        // Refresh 임시 목록
        private readonly HashSet<int> visibleIds = new HashSet<int>();
        private readonly List<int> leaving = new List<int>();

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

            // 한 번에 여러 줄이 바뀌면 사라지는 줄과 나타나는 줄이 겹치므로 두 배를 미리 만듦.
            for (int i = 0; i < maxRows * 2; i++)
            {
                pool.Push(Instantiate(rowTemplate, container));
            }

            background.sizeDelta = new Vector2(background.sizeDelta.x, 0.0F);
            background.gameObject.SetActive(false);
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

            // 로컬 플레이어가 표시 범위 밖이면 마지막 줄을 로컬 플레이어 순위로 씀.
            int localIndex = -1;

            for (int i = 0; i < ranking.Count; i++)
            {
                if (ranking[i].NetworkId == localNetworkId)
                {
                    localIndex = i;
                    break;
                }
            }

            int count = Mathf.Min(maxRows, ranking.Count);
            visibleIds.Clear();

            for (int slot = 0; slot < count; slot++)
            {
                int index = slot == maxRows - 1 && localIndex >= maxRows ? localIndex : slot;
                Entry entry = ranking[index];
                bool isLocal = index == localIndex;
                float y = -(padding + slot * (rowHeight + spacing));

                visibleIds.Add(entry.NetworkId);

                if (shown.TryGetValue(entry.NetworkId, out ScoreboardRow row))
                {
                    row.MoveTo(y, moveTime);
                }
                else
                {
                    row = Rent();
                    row.Place(y);
                    row.FadeIn(fadeTime);
                    shown.Add(entry.NetworkId, row);
                }

                Color rankColor = index < rankColors.Length ? rankColors[index] : nameColor;
                row.Set(index + 1, entry.Name, entry.Score, isLocal ? localNameColor : nameColor, rankColor, isLocal);
            }

            // 순위권에서 빠진 줄은 흐려진 뒤 반납.
            leaving.Clear();

            foreach (KeyValuePair<int, ScoreboardRow> pair in shown)
            {
                if (!visibleIds.Contains(pair.Key)) leaving.Add(pair.Key);
            }

            foreach (int networkId in leaving)
            {
                ScoreboardRow row = shown[networkId];
                shown.Remove(networkId);
                row.FadeOut(fadeTime, () => Return(row));
            }

            float height = count > 0 ? padding * 2.0F + count * rowHeight + (count - 1) * spacing : 0.0F;

            if (count > 0) background.gameObject.SetActive(true);

            background.DOKill();
            background.DOSizeDelta(new Vector2(background.sizeDelta.x, height), moveTime)
                .SetEase(Ease.OutQuad)
                .SetLink(gameObject)
                .OnComplete(() => background.gameObject.SetActive(height > 0.0F));
        }

        private ScoreboardRow Rent()
        {
            ScoreboardRow row = pool.Count > 0 ? pool.Pop() : Instantiate(rowTemplate, container);
            row.gameObject.SetActive(true);
            return row;
        }

        private void Return(ScoreboardRow row)
        {
            row.Kill();
            row.gameObject.SetActive(false);
            pool.Push(row);
        }

        // 점수 내림차순. 같으면 NetworkId 오름차순.
        private static int Compare(Entry a, Entry b)
        {
            int order = b.Score.CompareTo(a.Score);
            return order != 0 ? order : a.NetworkId.CompareTo(b.NetworkId);
        }
    }
}
