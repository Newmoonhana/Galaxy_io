using GalaxyIO.Tools.PopupUI.Popup;
using JWServer.Runtime.Login;
using Newmoonhana.Runtime.Tools.PopupUI.Window;
using Newmoonhana.Runtime.Utils.DEBUG;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.Localization;
using UnityEngine.Localization.Settings;
using UnityEngine.UI;

namespace GalaxyIO.Tools.PopupUI.Window
{
    /// <summary>
    /// 로비 메인 메뉴 창.
    /// </summary>
    public class WindowLobbyMain : WindowBase
    {
        [SerializeField] private Button signInOut_btn;
        [SerializeField] private TMP_Text signInOut_txt;
        [SerializeField] private Button connect_btn;
        [SerializeField] private Button host_btn;
        [SerializeField] private Button settings_btn;
        [Tooltip("크레딧. 다른 버튼보다 작게 배치함")]
        [SerializeField] private Button credit_btn;
        [SerializeField] private Button exit_btn;

        private UnityEvent<string> _changeWindow;

        protected override void Awake()
        {
            base.Awake();
            _changeWindow = new UnityEvent<string>();
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
            signInOut_btn.onClick.AddListener(OnSignInOutBtnPress);
            connect_btn.onClick.AddListener(OnConnectBtnPress);
            host_btn.onClick.AddListener(OnHostBtnPress);
            settings_btn.onClick.AddListener(OnSettingsBtnPress);
            credit_btn.onClick.AddListener(OnCreditBtnPress);
            exit_btn.onClick.AddListener(OnExitBtnPress);
            LocalizationSettings.SelectedLocaleChanged += OnLocaleChanged;
        }

        protected override void OnDisable()
        {
            base.OnDisable();
            signInOut_btn.onClick.RemoveListener(OnSignInOutBtnPress);
            connect_btn.onClick.RemoveListener(OnConnectBtnPress);
            host_btn.onClick.RemoveListener(OnHostBtnPress);
            settings_btn.onClick.RemoveListener(OnSettingsBtnPress);
            credit_btn.onClick.RemoveListener(OnCreditBtnPress);
            exit_btn.onClick.RemoveListener(OnExitBtnPress);
            LocalizationSettings.SelectedLocaleChanged -= OnLocaleChanged;
        }

        private void OnLocaleChanged(Locale _locale) => RefreshSignInOut();

        public override void Open()
        {
            base.Open();
            RefreshSignInOut();
        }

        public void AddListener(UnityAction<string> _action)
        {
            _changeWindow.AddListener(_action);
        }

        private void RefreshSignInOut()
        {
            signInOut_txt.SetText(L10n.Get(LoginManager.IsOnline ? "Lobby/SignOut" : "Lobby/SignIn"));
        }

        private void OnSignInOutBtnPress()
        {
            if (!IsActive()) return;

            if (!LoginManager.IsOnline)
            {
                _changeWindow?.Invoke("signIn");
                return;
            }

            LoginManager.LogOut();
            RefreshSignInOut();
        }

        private void OnConnectBtnPress()
        {
            if (!IsActive()) return;
            _changeWindow?.Invoke("serverBranch");
        }

        private void OnHostBtnPress()
        {
            if (!IsActive()) return;
            _changeWindow?.Invoke("serverHost");
        }

        private void OnSettingsBtnPress()
        {
            if (!IsActive()) return;
            OpenOption().Forget();
        }
        private async Awaitable OpenOption()
        {
            // 로드 중 중복 입력 방지. 열린 뒤엔 옵션 팝업의 BlockerUI가 입력 차단.
            Deactivate();
            await CommonOption.Open();
            Activate();
        }

        private void OnCreditBtnPress()
        {
            if (!IsActive()) return;
            _changeWindow?.Invoke("credit");
        }

        private void OnExitBtnPress()
        {
            if (!IsActive()) return;
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }
    }
}
