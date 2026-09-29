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
    /// 접속 대상 서버 종류. 공개 서버는 이름이 계정 기준으로 고정됨.
    /// </summary>
    public enum SERVERTYPE
    {
        PUBLIC,
        PRIVATE,
    }

    /// <summary>
    /// 서버 접속 및 플레이어 스폰을 요청하는 게임 시작 창. 비로그인도 접속 가능.
    /// </summary>
    public class WindowGameStart : WindowBase
    {
        [SerializeField] private ServerAddressField addressField;
        [SerializeField] private TMP_InputField playerName_input;
        [SerializeField] private TMP_Text warning;
        [SerializeField] private TMP_Text unreachable;
        [SerializeField] private TMP_Text nameWarning;
        [SerializeField] private Button startButton;
        [SerializeField] private Button loopbackButton;
        [SerializeField] private Button exitButton;

        // 직접 입력한 이름. 세션 동안 유지함.
        private static string sessionName = string.Empty;

        // 접속 대상 서버 종류. 창을 열기 전에 로비가 지정함.
        private SERVERTYPE serverType = SERVERTYPE.PRIVATE;

        // 이름을 직접 입력할 수 있는 상태.
        private bool nameEditable;

        // 접속에 성공해 게임 시작 창을 닫아도 되는 시점을 알림.
        private UnityEvent _connected;
        private UnityEvent<string> _changeWindow;

        private EntityQuery requestQuery;
        private EntityQuery failedQuery;
        private bool hasQueries;

        // 접속 결과를 기다리는 중.
        private bool connecting;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStaticData()
        {
            sessionName = string.Empty;
        }

        protected override void Awake()
        {
            base.Awake();
            _connected = new UnityEvent();
            _changeWindow = new UnityEvent<string>();
            playerName_input.characterLimit = PlayerNameComponent.MaxLength;
        }

        protected override void Start()
        {
            base.Start();

            warning.gameObject.SetActive(false);
            unreachable.gameObject.SetActive(false);
            nameWarning.gameObject.SetActive(false);

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
            nameWarning.gameObject.SetActive(false);
            RefreshName();
        }

        /// <summary>
        /// 접속 대상 서버 종류를 지정함. <see cref="Open"/> 전에 호출해야 함.
        /// </summary>
        public void SetServerType(SERVERTYPE _type)
        {
            serverType = _type;
        }

        protected override void OnEnable()
        {
            base.OnEnable();
            startButton.onClick.AddListener(OnPressGameStart);
            loopbackButton.onClick.AddListener(OnPressLoopback);
            exitButton.onClick.AddListener(OnPressBack);
            playerName_input.onValueChanged.AddListener(OnNameChanged);
        }

        protected override void OnDisable()
        {
            base.OnDisable();
            startButton.onClick.RemoveListener(OnPressGameStart);
            loopbackButton.onClick.RemoveListener(OnPressLoopback);
            exitButton.onClick.RemoveListener(OnPressBack);
            playerName_input.onValueChanged.RemoveListener(OnNameChanged);
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

            // 공백만 입력한 경우도 빈 이름으로 취급함.
            string requestPlayerName = playerName_input.text.Trim();

            if (requestPlayerName.Length == 0)
            {
                nameWarning.gameObject.SetActive(true);
                return;
            }

            nameWarning.gameObject.SetActive(false);
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
            playerName_input.interactable = !value && nameEditable;
        }

        /// <summary>
        /// 사설 서버에 비로그인으로 접속할 때만 이름을 직접 입력할 수 있음.
        /// 그 외에는 계정 닉네임/게스트 이름으로 고정함.
        /// </summary>
        private void RefreshName()
        {
            nameEditable = serverType == SERVERTYPE.PRIVATE && !LoginManager.IsOnline;
            playerName_input.interactable = nameEditable;

            // 편집 가능하면 세션에 남은 이름을, 없으면 게스트 이름을 제안값으로 채움.
            string name = nameEditable && !string.IsNullOrWhiteSpace(sessionName)
                ? sessionName
                : ResolvePlayerName();

            playerName_input.SetTextWithoutNotify(name);
        }

        // 편집 가능한 상태에서 입력한 이름만 세션에 남김.
        private void OnNameChanged(string value)
        {
            if (!nameEditable) return;

            sessionName = value;
            nameWarning.gameObject.SetActive(false);
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