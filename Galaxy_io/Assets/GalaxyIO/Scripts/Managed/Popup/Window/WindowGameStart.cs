using System;
using System.Net;
using System.Net.Sockets;
using GalaxyIO.DOTS.Bootstrap;
using GalaxyIO.DOTS.Client.Components.Message;
using GalaxyIO.DOTS.Client.Networking;
using GalaxyIO.DOTS.Components.Actor;
using GalaxyIO.Scripts.Managed.UI;
using JWServer.Runtime.Login;
using Newmoonhana.Runtime.Tools.PopupUI.Window;
using TMPro;
using Unity.Entities;
using Unity.NetCode;
using Unity.Networking.Transport;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;
using Random = UnityEngine.Random;

namespace GalaxyIO.Tools.PopupUI.Window
{
    /// <summary>
    /// 서버 접속 및 플레이어 스폰을 요청하는 게임 시작 창. 비로그인도 접속 가능.
    /// </summary>
    public class WindowGameStart : WindowBase
    {
        [SerializeField] private ServerAddressField addressField;
        [SerializeField] private TMP_Text playerName_txt;
        [SerializeField] private TMP_InputField playerName_input;
        [SerializeField] private TMP_Text warning;
        [SerializeField] private TMP_Text unreachable;
        [SerializeField] private Button startButton;
        [SerializeField] private Button loopbackButton;
        [SerializeField] private Button exitButton;

        // 이번에 창을 열 때 확정한 이름.
        private string playerName;

        // 접속에 성공해 게임 시작 창을 닫아도 되는 시점을 알림.
        private UnityEvent _connected;
        private UnityEvent<string> _changeWindow;

        private EntityQuery requestQuery;
        private EntityQuery failedQuery;
        private bool hasQueries;

        // 접속 결과를 기다리는 중.
        private bool connecting;

        protected override void Awake()
        {
            base.Awake();
            _connected = new UnityEvent();
            _changeWindow = new UnityEvent<string>();
        }

        protected override void Start()
        {
            base.Start();

            warning.gameObject.SetActive(false);
            unreachable.gameObject.SetActive(false);

            World clientWorld = ClientServerBootstrap.ClientWorld;
            if (clientWorld == null) return;

            requestQuery = clientWorld.EntityManager.CreateEntityQuery(
                ComponentType.ReadOnly<PlayerSpawnRequest>()
            );
            failedQuery = clientWorld.EntityManager.CreateEntityQuery(
                ComponentType.ReadOnly<PlayerConnectionFailedTag>()
            );
            hasQueries = true;
        }

        public override void Dispose()
        {
            base.Dispose();
            _connected?.RemoveAllListeners();
            _connected = null;
            _changeWindow?.RemoveAllListeners();
            _changeWindow = null;
        }

        public override void Open()
        {
            base.Open();
            playerName = ResolvePlayerName();
            playerName_txt.SetText(playerName);
        }

        protected override void OnEnable()
        {
            base.OnEnable();
            startButton.onClick.AddListener(OnPressGameStart);
            loopbackButton.onClick.AddListener(OnPressLoopback);
            exitButton.onClick.AddListener(OnPressBack);
        }

        protected override void OnDisable()
        {
            base.OnDisable();
            startButton.onClick.RemoveListener(OnPressGameStart);
            loopbackButton.onClick.RemoveListener(OnPressLoopback);
            exitButton.onClick.RemoveListener(OnPressBack);
        }

        /// <summary>
        /// 서버 접속(플레이어 스폰 요청)에 성공했을 때 알림 받을 리스너 등록.
        /// </summary>
        public void AddListener(UnityAction _action)
        {
            _connected.AddListener(_action);
        }

        public void AddListener(UnityAction<string> _action)
        {
            _changeWindow.AddListener(_action);
        }

        private void Update()
        {
            if (!hasQueries || !connecting) return;

            // Handshake 전에 끊긴 접속은 접속 실패.
            if (!failedQuery.IsEmpty)
            {
                ClientServerBootstrap.ClientWorld.EntityManager.DestroyEntity(failedQuery);
                unreachable.gameObject.SetActive(true);
                SetConnecting(false);
                return;
            }

            // 요청이 처리됐고 실패 태그가 없으면 접속 성공.
            if (requestQuery.IsEmpty)
            {
                SetConnecting(false);
                _connected?.Invoke();
            }
        }

        /// <summary>
        /// 로비 메인으로 복귀. [TEMP] 버튼 onClick 연결용.
        /// </summary>
        public void OnPressBack()
        {
            if (!IsActive() || connecting) return;
            _changeWindow?.Invoke("main");
        }

        /// <summary>
        /// 루프백 주소를 입력창에 채움. 접속은 하지 않음.
        /// </summary>
        private void OnPressLoopback()
        {
            if (!IsActive()) return;

            addressField.Fill(NetcodeBootstrap.LoopbackAddress);
            warning.gameObject.SetActive(false);
            unreachable.gameObject.SetActive(false);
        }

        /// <summary>
        /// 스폰을 요청함. 접속에 성공하면 창이 닫힘.
        /// </summary>
        private void OnPressGameStart()
        {
            if (!IsActive()) return;

            unreachable.gameObject.SetActive(false);

            // 서버에 요청할 플레이어 이름. 입력 필드가 없으면 화면을 열 때 설정한 `playerName`으로 자동으로 설정하고,
            // 따로 입력한 이름이 있다면 그 이름으로 요청함.
            string requestPlayerName = string.IsNullOrWhiteSpace(playerName_input.text) ? playerName : playerName_input.text.Trim();
            _ = ConnectAsync(requestPlayerName);
        }

        /// <summary>
        /// 주소를 해석한 뒤 스폰을 요청함. 해석 중에도 버튼을 잠금.
        /// </summary>
        private async Awaitable ConnectAsync(string requestPlayerName)
        {
            SetLocked(true);

            NetworkEndpoint? endpoint = await ResolveAsync(addressField.Address);

            if (endpoint == null)
            {
                warning.gameObject.SetActive(true);
                SetLocked(false);
                return;
            }

            warning.gameObject.SetActive(false);

            if (ClientNetworkRequestManager.RequestPlayerSpawn(endpoint.Value, requestPlayerName))
            {
                SetConnecting(true);
            }
            else
            {
                SetLocked(false);
            }
        }

        /// <summary>
        /// IP 주소는 그대로, 도메인은 DNS 조회로 IPv4 엔드포인트를 만듦. 실패하면 null.
        /// </summary>
        private static async Awaitable<NetworkEndpoint?> ResolveAsync(string address)
        {
            // 모양이 맞아도 값이 범위를 넘으면(e.g. 999.0.0.1) 여기서 걸림.
            if (NetworkEndpoint.TryParse(address, NetcodeBootstrap.Port, out NetworkEndpoint endpoint, NetworkFamily.Ipv4))
            {
                return endpoint;
            }

            if (Uri.CheckHostName(address) != UriHostNameType.Dns)
            {
                return null;
            }

            IPAddress[] addresses = null;

            try
            {
                addresses = await Dns.GetHostAddressesAsync(address);
            }
            catch (Exception)
            {
                // 조회 실패. 아래에서 null 반환.
            }

            await Awaitable.MainThreadAsync();

            if (addresses == null)
            {
                return null;
            }

            foreach (IPAddress ip in addresses)
            {
                if (ip.AddressFamily == AddressFamily.InterNetwork)
                {
                    return NetworkEndpoint.Parse(ip.ToString(), NetcodeBootstrap.Port);
                }
            }

            return null;
        }

        /// <summary>
        /// 접속 대기 상태 전환. 대기 중에는 버튼을 잠금.
        /// </summary>
        private void SetConnecting(bool value)
        {
            connecting = value;
            SetLocked(value);
        }

        private void SetLocked(bool value)
        {
            startButton.interactable = !value;
            loopbackButton.interactable = !value;
        }

        /// <summary>
        /// 로그인 유저는 계정 닉네임, 비로그인 유저는 게스트 이름.
        /// </summary>
        private string ResolvePlayerName()
        {
            string nickName = LoginManager.NickName;

            // 닉네임 조회 실패 시에도 게스트 이름 사용. 창 열 때마다 새 번호.
            if (!LoginManager.IsOnline || string.IsNullOrEmpty(nickName))
                return $"Pilot#{Random.Range(0, 10000):D4}";

            return nickName.Length > PlayerNameComponent.MaxLength
                ? nickName.Substring(0, PlayerNameComponent.MaxLength)
                : nickName;
        }
    }
}