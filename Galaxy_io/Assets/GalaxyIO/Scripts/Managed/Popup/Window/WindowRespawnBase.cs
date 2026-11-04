using System;
using GalaxyIO.Tools.PopupUI.Popup;
using Newmoonhana.Runtime.Tools.PopupUI.Window;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace GalaxyIO.Tools.PopupUI.Window
{
    /// <summary>
    /// 리스폰 창의 공통부. 나가기 버튼, 웹 전용 크레딧/설정 버튼.
    /// </summary>
    public abstract class WindowRespawnBase : WindowBase
    {
        [SerializeField] private Button quit_btn;

        [Header("Web")]
        [Tooltip("웹 빌드에서만 켜는 크레딧 버튼")]
        [SerializeField] private Button credit_btn;
        [Tooltip("웹 빌드에서만 켜는 설정 버튼")]
        [SerializeField] private Button settings_btn;

        private UnityEvent _quit;

        protected override void Awake()
        {
            base.Awake();
            _quit = new UnityEvent();

#if UNITY_WEBGL && !UNITY_EDITOR
            quit_btn.gameObject.SetActive(false);
#else
            credit_btn.gameObject.SetActive(false);
            settings_btn.gameObject.SetActive(false);
#endif
        }

        public override void Dispose()
        {
            base.Dispose();
            _quit?.RemoveAllListeners();
            _quit = null;
        }

        protected override void OnEnable()
        {
            base.OnEnable();
            quit_btn.onClick.AddListener(OnQuitBtnPress);
            credit_btn.onClick.AddListener(OnCreditBtnPress);
            settings_btn.onClick.AddListener(OnSettingsBtnPress);
        }

        protected override void OnDisable()
        {
            base.OnDisable();
            quit_btn.onClick.RemoveListener(OnQuitBtnPress);
            credit_btn.onClick.RemoveListener(OnCreditBtnPress);
            settings_btn.onClick.RemoveListener(OnSettingsBtnPress);
        }

        public void AddListener(UnityAction _onQuit)
        {
            _quit.AddListener(_onQuit);
        }

        private void OnQuitBtnPress()
        {
            if (!IsActive()) return;
            _quit?.Invoke();
        }

        private void OnCreditBtnPress()
        {
            if (!IsActive()) return;
            OpenPopup(CommonCredit.Open).Forget();
        }

        private void OnSettingsBtnPress()
        {
            if (!IsActive()) return;
            OpenPopup(CommonOption.Open).Forget();
        }

        private async Awaitable OpenPopup(Func<Awaitable> _open)
        {
            // 로드 중 중복 입력 방지. 열린 뒤엔 팝업의 BlockerUI가 입력 차단.
            Deactivate();
            await _open();
            Activate();
        }
    }
}
