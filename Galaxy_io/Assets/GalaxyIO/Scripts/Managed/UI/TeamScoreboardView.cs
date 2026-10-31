using System.Collections.Generic;
using GalaxyIO.DOTS.Authoring;
using GalaxyIO.DOTS.Client.Components.Singleton;
using GalaxyIO.DOTS.Client.Networking;
using GalaxyIO.DOTS.Components.Message;
using GalaxyIO.DOTS.Components.Singleton;
using GalaxyIO.Tools;
using TMPro;
using Unity.Collections;
using Unity.Entities;
using Unity.NetCode;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace GalaxyIO.Scripts.Managed.UI
{
    /// <summary>
    /// 팀 모드에서 Tab 을 누르고 있는 동안 보이는 점수판. 팀별로 나눠 처치, 어시스트, 사망 순으로 줄을 세움.
    /// 옮긴 뒤 두 팀 인원 차이가 3 이하면 상대 팀으로 옮길 수 있음. 판이 끝나면 승리/패배 로고와 나가기 버튼을 함께 다음 판까지 띄워 둠.
    /// 열려 있는 동안 뒤 화면을 어둡게 가림.
    /// </summary>
    public sealed class TeamScoreboardView : MonoBehaviour
    {
        [SerializeField]
        [Tooltip("Tab 을 누르는 동안만 켜는 판")]
        private GameObject panel;

        [SerializeField]
        [Tooltip("판과 함께 켜는 반투명 검은 화면")]
        private GameObject dimmer;

        [SerializeField]
        [Tooltip("씬에 배치된 점수판 한 줄 템플릿")]
        private TeamScoreboardRow rowTemplate;

        [SerializeField]
        [Tooltip("빨강 팀 줄이 쌓이는 부모. 스크롤 영역의 Content.")]
        private RectTransform redContainer;

        [SerializeField]
        [Tooltip("파랑 팀 줄이 쌓이는 부모. 스크롤 영역의 Content.")]
        private RectTransform blueContainer;

        [SerializeField]
        private TMP_Text redScoreLabel;

        [SerializeField]
        private TMP_Text blueScoreLabel;

        [SerializeField]
        private Button switchButton;

        [SerializeField]
        [Tooltip("SpaceshipCategory 순서의 계급장")]
        private Sprite[] badges;

        [Header("Result")]

        [SerializeField]
        [Tooltip("판이 끝나면 점수판 위에 켜는 결과 제목")]
        private GameObject resultPanel;

        [SerializeField]
        private Image resultLogo;

        [SerializeField]
        [Tooltip("로고가 없을 때 대신 보이는 승리/패배 글씨")]
        private TMP_Text resultTitle;

        [SerializeField]
        [Tooltip("이긴 팀 안내")]
        private TMP_Text resultWinner;

        [SerializeField]
        private Sprite victoryLogo;

        [SerializeField]
        private Sprite defeatLogo;

        [SerializeField]
        [Tooltip("결과 화면에서만 보이는 나가기 버튼")]
        private Button exitButton;

        [Header("Colors")]

        [SerializeField]
        private Color nameColor = Color.white;

        [SerializeField]
        [Tooltip("로컬 플레이어 이름 색")]
        private Color localNameColor = new Color(1.0F, 0.85F, 0.3F);

        // 옮긴 뒤 두 팀 인원 차이의 상한. 서버와 같은 값.
        private const int MaxTeamGap = 3;

        public struct Entry
        {
            public int NetworkId;
            public string Name;
            public Team Team;
            public SpaceshipId Spaceship;
            public int Kills;
            public int Assists;
            public int Deaths;
            public int Ping;
        }

        private EntityManager entityManager;
        private EntityQuery messageQuery;
        private EntityQuery connectionQuery;
        private EntityQuery stateQuery;
        private EntityQuery registryQuery;
        private EntityQuery playerStateQuery;
        private EntityQuery modeQuery;

        // 캐시한 쿼리가 속한 클라이언트 월드. 세션 종료 시 Dispose 되므로 매 프레임 확인함.
        private World clientWorld;

        // 결과 화면에 보여 준 남은 초. 바뀔 때만 글자를 다시 만듦.
        private int shownCountdown = -1;

        // NetworkId 별 줄
        private readonly Dictionary<int, Entry> entries = new Dictionary<int, Entry>();
        private readonly List<Entry> ranking = new List<Entry>();
        private readonly List<TeamScoreboardRow> redRows = new List<TeamScoreboardRow>();
        private readonly List<TeamScoreboardRow> blueRows = new List<TeamScoreboardRow>();

        private bool dirty;

        /// <summary>
        /// 받은 점수판 줄. NetworkId 별 하나.
        /// </summary>
        public IReadOnlyCollection<Entry> Entries => entries.Values;

        /// <summary>
        /// <see cref="Entries"/>가 바뀔 때마다 늘어남.
        /// </summary>
        public int Version { get; private set; }

        private void Awake()
        {
            rowTemplate.gameObject.SetActive(false);
        }

        private void Start()
        {
            panel.SetActive(false);
            dimmer.SetActive(false);
            resultPanel.SetActive(false);
            clientWorld = ClientServerBootstrap.ClientWorld;

            if (clientWorld == null)
            {
                enabled = false;
                return;
            }

            entityManager = clientWorld.EntityManager;
            messageQuery = entityManager.CreateEntityQuery(
                ComponentType.ReadOnly<PlayerStatRPC>(),
                ComponentType.ReadOnly<ReceiveRpcCommandRequest>()
            );
            connectionQuery = entityManager.CreateEntityQuery(ComponentType.ReadOnly<NetworkId>());
            stateQuery = entityManager.CreateEntityQuery(ComponentType.ReadOnly<GameModeStateComponent>());
            registryQuery = entityManager.CreateEntityQuery(ComponentType.ReadOnly<SpaceshipRegistryAssetComponent>());
            playerStateQuery = entityManager.CreateEntityQuery(ComponentType.ReadOnly<PlayerStateComponent>());
            modeQuery = entityManager.CreateEntityQuery(ComponentType.ReadOnly<GameModeComponent>());

            switchButton.onClick.AddListener(ClientNetworkRequestManager.RequestTeamSwitch);
            exitButton.onClick.AddListener(Quit);
        }

        private void OnDestroy()
        {
            if (switchButton != null) switchButton.onClick.RemoveListener(ClientNetworkRequestManager.RequestTeamSwitch);
            if (exitButton != null) exitButton.onClick.RemoveListener(Quit);
        }

        private void LateUpdate()
        {
            // 세션을 나가면 클라이언트 월드가 Dispose 됨. 캐시한 쿼리를 더 쓰면 안 됨.
            if (!ClientWorldScope.IsAlive(clientWorld))
            {
                panel.SetActive(false);
                dimmer.SetActive(false);
                resultPanel.SetActive(false);
                enabled = false;
                return;
            }

            Receive();

            // 자유 모드에는 팀 점수판이 없음.
            bool teamMode = !modeQuery.IsEmpty;

            GameModeStateComponent state = stateQuery.GetSingleton<GameModeStateComponent>();
            bool finished = teamMode && state.Winner != Team.None && !connectionQuery.IsEmpty;

            if (resultPanel.activeSelf != finished)
            {
                resultPanel.SetActive(finished);
                exitButton.gameObject.SetActive(finished);
                shownCountdown = -1;
            }

            // 다음 판까지 남은 초. 바뀔 때만 글자를 다시 만듦.
            if (finished)
            {
                int countdown = Mathf.Max(Mathf.CeilToInt((float)(state.RestartAt - clientWorld.Time.ElapsedTime)), 0);

                if (countdown != shownCountdown)
                {
                    shownCountdown = countdown;
                    ShowResult(state, countdown);
                }
            }

            // 채팅 입력 중의 Tab 은 무시함. 판이 끝나면 다음 판까지 띄워 둠.
            bool chatting = !playerStateQuery.IsEmpty && playerStateQuery.GetSingleton<PlayerStateComponent>().ChatFocused;
            bool open = finished || (teamMode && Keyboard.current != null && Keyboard.current.tabKey.isPressed && !chatting && !connectionQuery.IsEmpty);

            if (panel.activeSelf != open)
            {
                panel.SetActive(open);
                dimmer.SetActive(open);
                dirty |= open;
            }

            if (!open || !dirty) return;

            dirty = false;
            Refresh();
        }

        private void Receive()
        {
            // 접속이 끊기면 비움. 다시 입장하면 서버가 전체를 다시 보냄.
            if (connectionQuery.IsEmpty)
            {
                entityManager.DestroyEntity(messageQuery);

                if (entries.Count > 0)
                {
                    entries.Clear();
                    Version++;
                    dirty = true;
                }

                return;
            }

            if (messageQuery.IsEmpty) return;

            using var messages = messageQuery.ToComponentDataArray<PlayerStatRPC>(Allocator.Temp);

            foreach (PlayerStatRPC message in messages)
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
                    Team      = message.Team,
                    Spaceship = message.Spaceship,
                    Kills     = message.Kills,
                    Assists   = message.Assists,
                    Deaths    = message.Deaths,
                    Ping      = message.Ping,
                };
            }

            entityManager.DestroyEntity(messageQuery);
            Version++;
            dirty = true;
        }

        private static void Quit()
        {
            GalaxyIO.Main.GalaxyIO_Main.Instance.QuitGame();
        }

        // 우리 팀이 이겼으면 승리, 아니면 패배. 로고가 없으면 글씨로 띄움. 이긴 팀 아래에 다음 판까지 남은 시간.
        private void ShowResult(in GameModeStateComponent state, int countdown)
        {
            bool victory = state.Winner == state.Team;
            Sprite logo = victory ? victoryLogo : defeatLogo;

            resultLogo.sprite = logo;
            resultLogo.enabled = logo != null;

            resultTitle.enabled = logo == null;
            resultTitle.text = L10n.Get(victory ? "Team/Victory" : "Team/Defeat");

            resultWinner.text = L10n.Get(state.Winner == Team.Red ? "Team/RedWins" : "Team/BlueWins")
                                + $"\n<size=60%>{string.Format(L10n.Get("Team/NextGame"), countdown)}</size>";
        }

        private void Refresh()
        {
            int localNetworkId = connectionQuery.GetSingleton<NetworkId>().Value;
            SpaceshipRegistryAssetComponent registry = registryQuery.IsEmpty
                ? null
                : registryQuery.GetSingleton<SpaceshipRegistryAssetComponent>();

            ranking.Clear();
            ranking.AddRange(entries.Values);
            ranking.Sort(Compare);

            int red = 0;
            int blue = 0;

            foreach (Entry entry in ranking)
            {
                bool isRed = entry.Team == Team.Red;
                List<TeamScoreboardRow> rows = isRed ? redRows : blueRows;
                int index = isRed ? red++ : blue++;

                if (index >= rows.Count)
                {
                    rows.Add(Instantiate(rowTemplate, isRed ? redContainer : blueContainer));
                }

                SpaceshipAsset spaceship = registry?.Value != null ? registry.Value.Find(entry.Spaceship) : null;
                Sprite badge = spaceship != null && (int)spaceship.Category < badges.Length ? badges[(int)spaceship.Category] : null;

                rows[index].gameObject.SetActive(true);
                rows[index].Set(
                    badge,
                    entry.Name,
                    entry.NetworkId == localNetworkId ? localNameColor : nameColor,
                    entry.Kills, entry.Assists, entry.Deaths, entry.Ping
                );
            }

            for (int i = red; i < redRows.Count; i++) redRows[i].gameObject.SetActive(false);
            for (int i = blue; i < blueRows.Count; i++) blueRows[i].gameObject.SetActive(false);

            GameModeStateComponent state = stateQuery.GetSingleton<GameModeStateComponent>();
            redScoreLabel.text = state.RedScore.ToString();
            blueScoreLabel.text = state.BlueScore.ToString();

            // 옮긴 뒤 인원 차이가 상한 이하일 때만 옮길 수 있음.
            int own = state.Team == Team.Red ? red : blue;
            int other = state.Team == Team.Red ? blue : red;
            switchButton.interactable = state.Team != Team.None && Mathf.Abs((own - 1) - (other + 1)) <= MaxTeamGap;
        }

        // 처치 내림차순, 어시스트 내림차순, 사망 오름차순. 같으면 NetworkId 오름차순.
        private static int Compare(Entry a, Entry b)
        {
            int order = b.Kills.CompareTo(a.Kills);
            if (order != 0) return order;

            order = b.Assists.CompareTo(a.Assists);
            if (order != 0) return order;

            order = a.Deaths.CompareTo(b.Deaths);
            return order != 0 ? order : a.NetworkId.CompareTo(b.NetworkId);
        }
    }
}
