using GalaxyIO.DOTS.Bootstrap;
using GalaxyIO.DOTS.Client.Components.Message;
using GalaxyIO.DOTS.Client.Networking;
using GalaxyIO.Scripts.Managed.UI;
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
    /// 로그인 성공 후 서버 접속 및 플레이어 스폰을 요청하는 게임 시작 대기 창.
    /// </summary>
    public class WindowGameStart : WindowBase
    {
        [SerializeField] private IpAddressField addressField;
        [SerializeField] private TMP_Text warning;
        [SerializeField] private TMP_Text unreachable;
        [SerializeField] private Button startButton;
        [SerializeField] private Button loopbackButton;

        // 접속에 성공해 게임 시작 창을 닫아도 되는 시점을 알림.
        private UnityEvent _connected;

        private EntityQuery requestQuery;
        private EntityQuery failedQuery;
        private bool hasQueries;

        // 접속 결과를 기다리는 중.
        private bool connecting;

        protected override void Awake()
        {
            base.Awake();
            _connected = new UnityEvent();
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
        }

        /// <summary>
        /// 스폰을 요청함. 접속에 성공하면 창이 닫힘.
        /// </summary>
        private void OnPressGameStart()
        {
            if (!IsActive()) return;

            unreachable.gameObject.SetActive(false);

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

            // TODO: "닉네임" 파라미터 가져오기, 사실 이 모듈이 삭제될 예정이라 쓸일 없을것 같긴함
            // if (ClientNetworkRequestManager.RequestPlayerSpawn(endpoint))
            // {
            //    warning.gameObject.SetActive(false);
            //    SetConnecting(true);
            // }
        }

        /// <summary>
        /// 접속 대기 상태 전환. 대기 중에는 버튼을 잠금.
        /// </summary>
        private void SetConnecting(bool value)
        {
            connecting = value;
            startButton.interactable = !value;
            loopbackButton.interactable = !value;
        }
    }
}