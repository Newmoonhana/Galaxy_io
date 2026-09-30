using Newmoonhana.Runtime.Tools.PopupUI.Window;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace GalaxyIO.Tools.PopupUI.Window
{
    /// <summary>
    /// 접속할 서버 종류(공개/사설)를 고르는 창.
    /// </summary>
    public class WindowServerBranch : WindowBase
    {
        [SerializeField] private Button public_btn;
        [SerializeField] private Button private_btn;
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
            public_btn.onClick.AddListener(OnPressPublic);
            private_btn.onClick.AddListener(OnPressPrivate);
            exit_btn.onClick.AddListener(OnPressBack);
        }

        protected override void OnDisable()
        {
            base.OnDisable();
            public_btn.onClick.RemoveListener(OnPressPublic);
            private_btn.onClick.RemoveListener(OnPressPrivate);
            exit_btn.onClick.RemoveListener(OnPressBack);
        }

        public void AddListener(UnityAction<string> _action)
        {
            _changeWindow.AddListener(_action);
        }

        private void OnPressPublic()
        {
            if (!IsActive()) return;
            _changeWindow?.Invoke("gameStartPublic");
        }

        private void OnPressPrivate()
        {
            if (!IsActive()) return;
            _changeWindow?.Invoke("gameStartPrivate");
        }

        private void OnPressBack()
        {
            if (!IsActive()) return;
            _changeWindow?.Invoke("main");
        }
    }
}
