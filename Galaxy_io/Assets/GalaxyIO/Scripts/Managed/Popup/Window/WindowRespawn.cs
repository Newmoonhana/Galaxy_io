using GalaxyIO.DOTS.Client.Networking;
using Newmoonhana.Runtime.Tools.PopupUI.Window;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace GalaxyIO.Tools.PopupUI.Window
{
    public class WindowRespawn : WindowBase
    {
        [SerializeField] private Button quit_btn;

        private UnityEvent _quit;

        protected override void Awake()
        {
            base.Awake();
            _quit = new UnityEvent();
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
        }

        protected override void OnDisable()
        {
            base.OnDisable();
            quit_btn.onClick.RemoveListener(OnQuitBtnPress);
        }

        public void AddListener(UnityAction _onQuit)
        {
            _quit.AddListener(_onQuit);
        }

        /// <summary>
        /// 리스폰 버튼. 요청이 접수되면 태그가 지워져 창이 닫힘.
        /// </summary>
        public void OnPressRespawn()
        {
            ClientNetworkRequestManager.RequestRespawn();
        }

        private void OnQuitBtnPress()
        {
            if (!IsActive()) return;
            _quit?.Invoke();
        }
    }

}
