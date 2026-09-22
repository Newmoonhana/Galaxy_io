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

namespace GalaxyIO.Tools.PopupUI.Window
{
    /// <summary>
    /// 서버 접속 및 플레이어 스폰을 요청하는 게임 시작 창. 비로그인도 접속 가능.
    /// </summary>
    public class WindowGameStart : WindowBase
    {
        [SerializeField] private IpAddressField addressField;
        [SerializeField] private TMP_InputField playerName_if;
        [SerializeField] private TMP_Text nameWarning;
        [SerializeField] private TMP_Text warning;
        [SerializeField] private TMP_Text unreachable;
        [SerializeField] private Button startButton;
        [SerializeField] private Button loopbackButton;

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
            playerName_if.characterLimit = PlayerNameComponent.MaxLength;
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
            // 로그인 유저는 로그인 id를 제안값으로 채움. 수정 가능, 서버 계정과 무관.
            playerName_if.text = LoginManager.UserId;
        }

        protected override void OnEnable()
        {
            base.OnEnable();
            startButton.onClick.AddListener(OnPressGameStart);
            loopbackButton.onClick.AddListener(OnPressLoopback);
        }

        protected override void OnDisable()
        {
            base.OnDisable();
            startButton.onClick.RemoveListener(OnPressGameStart);
            loopbackButton.onClick.RemoveListener(OnPressLoopback);
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

        /// <summary>
        /// 로비 메인으로 복귀. [TEMP] 버튼 onClick 연결용.
        /// </summary>
        public void OnPressBack()
        {
            if (!IsActive() || connecting) return;
            _changeWindow?.Invoke("main");
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
        /// 루프백 주소를 입력창에 채움. 접속은 하지 않음.
        /// </summary>
        private void OnPressLoopback()
        {
            if (!IsActive()) return;

            addressField.Fill(NetcodeBootstrap.LoopbackAddress);
            warning.gameObject.SetActive(false);
            unreachable.gameObject.SetActive(false);
            nameWarning.gameObject.SetActive(false);
        }

        /// <summary>
        /// 스폰을 요청함. 접속에 성공하면 창이 닫힘.
        /// </summary>
        private void OnPressGameStart()
        {
            if (!IsActive()) return;

            unreachable.gameObject.SetActive(false);

            // 공백만 입력한 경우도 빈 이름으로 취급함.
            string playerName = playerName_if.text.Trim();

            if (playerName.Length == 0)
            {
                warning.gameObject.SetActive(false);
                nameWarning.gameObject.SetActive(true);
                return;
            }

            nameWarning.gameObject.SetActive(false);

            // 모양이 맞아도 값이 범위를 넘으면(e.g. 999.0.0.1) 여기서 걸림.
            if (!NetworkEndpoint.TryParse(
                    addressField.Address,
                    NetcodeBootstrap.Port,
                    out NetworkEndpoint endpoint,
                    NetworkFamily.Ipv4
                ))
            {
                warning.gameObject.SetActive(true);
                return;
            }

            if (ClientNetworkRequestManager.RequestPlayerSpawn(endpoint, playerName))
            {
                warning.gameObject.SetActive(false);
                SetConnecting(true);
            }
        }

        /// <summary>
        /// 접속 대기 상태 전환. 대기 중에는 버튼을 잠금.
        /// </summary>
        private void SetConnecting(bool value)
        {
            connecting = value;
            startButton.interactable = !value;
            loopbackButton.interactable = !value;
            playerName_if.interactable = !value;
        }
    }
}