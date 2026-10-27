using System;
using System.Net;
using System.Net.Sockets;
using GalaxyIO.DOTS.Bootstrap;
using GalaxyIO.DOTS.Networking;
using GalaxyIO.Tools.PopupUI.Popup;
using Newmoonhana.Runtime.Tools.PopupUI.Window;
using TMPro;
using Unity.Networking.Transport;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace GalaxyIO.Tools.PopupUI.Window
{
    /// <summary>
    /// 서버 접속과 플레이어 스폰을 요청하는 게임 시작 창의 공통부.
    /// 주소와 이름을 어디서 가져올지는 공개/사설 창이 정함.
    /// </summary>
    public abstract class WindowGameStartBase : WindowBase
    {
        [SerializeField] protected TMP_Text warning;
        [SerializeField] protected Button startButton;
        [SerializeField] protected Button exitButton;

        private UnityEvent<string> _changeWindow;

        // 주소 해석·씬 로드를 기다리는 중.
        protected bool busy;

        /// <summary>
        /// 접속할 서버 주소. IP 또는 도메인.
        /// </summary>
        protected abstract string Address { get; }

        protected override void Awake()
        {
            base.Awake();
            _changeWindow = new UnityEvent<string>();
        }

        protected override void Start()
        {
            base.Start();
            warning.gameObject.SetActive(false);
        }

        public override void Dispose()
        {
            base.Dispose();
            _changeWindow?.RemoveAllListeners();
            _changeWindow = null;
        }

        protected override void OnEnable()
        {
            base.OnEnable();
            startButton.onClick.AddListener(OnPressGameStart);
            exitButton.onClick.AddListener(OnPressBack);

            // 접속 실패로 로비 씬이 되살아난 경우. 떠날 때 건 잠금을 품.
            SetLocked(false);
        }

        protected override void OnDisable()
        {
            base.OnDisable();
            startButton.onClick.RemoveListener(OnPressGameStart);
            exitButton.onClick.RemoveListener(OnPressBack);
        }

        public void AddListener(UnityAction<string> _action)
        {
            _changeWindow.AddListener(_action);
        }

        /// <summary>
        /// 서버 분기 창으로 복귀. 버튼 onClick 연결용.
        /// </summary>
        public void OnPressBack()
        {
            if (!IsActive() || busy) return;
            _changeWindow?.Invoke("serverBranch");
        }

        /// <summary>나가기 버튼과 같은 처리. ESC 용.</summary>
        public void Cancel() => OnPressBack();

        /// <summary>
        /// 접속에 쓸 이름. 입력이 잘못되면 창이 직접 안내를 띄우고 null 을 돌려줌.
        /// </summary>
        protected abstract string TakePlayerName();

        /// <summary>
        /// 창별 추가 입력 잠금.
        /// </summary>
        protected virtual void OnLock(bool value) {}

        /// <summary>
        /// 대기 중 입력 잠금.
        /// </summary>
        protected void SetLocked(bool value)
        {
            busy = value;
            startButton.interactable = !value;
            OnLock(value);
        }

        /// <summary>
        /// 스폰을 요청함. 접속에 성공하면 인게임 씬이 창을 덮음.
        /// </summary>
        private void OnPressGameStart()
        {
            if (!IsActive() || busy) return;

            string requestPlayerName = TakePlayerName();
            if (requestPlayerName == null) return;

            ConnectAsync(requestPlayerName).Forget();
        }

        /// <summary>
        /// 주소를 해석한 뒤 스폰을 요청함. 해석 중에도 버튼을 잠금.
        /// </summary>
        private async Awaitable ConnectAsync(string requestPlayerName)
        {
            SetLocked(true);

            NetworkEndpoint? endpoint = await ResolveAsync(Address);

            if (endpoint == null)
            {
                warning.gameObject.SetActive(true);
                SetLocked(false);
                return;
            }

            warning.gameObject.SetActive(false);

            // 씬 로드·스트리밍·접속 동안 빈 화면이 보이지 않게 가림. 인게임에서 치움.
            CommonProgress.ShowAcrossScenes().Forget();

            // 월드 생성 → 인게임 씬 로드 → 접속까지 `GameSession`이 처리함.
            // 이 창은 로비 씬과 함께 꺼져 있다가 접속 실패 시 그대로 되살아남.
            await GameSession.JoinAsClient(endpoint.Value, requestPlayerName);
        }

        /// <summary>
        /// IP 주소는 그대로, 도메인은 DNS 조회로 IPv4 엔드포인트를 만듦. 실패하면 null.
        /// </summary>
        protected static async Awaitable<NetworkEndpoint?> ResolveAsync(string address)
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

#if UNITY_WEBGL && !UNITY_EDITOR
            // 브라우저는 DNS 조회를 막음. 웹에서는 IP 주소만 받음.
            return null;
#else
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
#endif
        }
    }
}
