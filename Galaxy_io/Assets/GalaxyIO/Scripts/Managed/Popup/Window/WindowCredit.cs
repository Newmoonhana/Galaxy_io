using Newmoonhana.Runtime.Tools.PopupUI.Window;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace GalaxyIO.Tools.PopupUI.Window
{
    /// <summary>
    /// 크레딧 창. 나가기만 가짐.
    /// </summary>
    public class WindowCredit : WindowBase
    {
        [SerializeField] private Button exit_btn;
        [Tooltip("창을 다시 열 때 맨 위부터 보여 주기 위한 스크롤")]
        [SerializeField] private ScrollRect desc_scroll;

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
            exit_btn.onClick.AddListener(OnPressBack);
        }

        protected override void OnDisable()
        {
            base.OnDisable();
            exit_btn.onClick.RemoveListener(OnPressBack);
        }

        public void AddListener(UnityAction<string> _action)
        {
            _changeWindow.AddListener(_action);
        }

        public override void Open()
        {
            base.Open();
            desc_scroll.verticalNormalizedPosition = 1F;
        }

        private void OnPressBack()
        {
            if (!IsActive()) return;
            _changeWindow?.Invoke("main");
        }

        /// <summary>나가기 버튼과 같은 처리. ESC 용.</summary>
        public void Cancel() => OnPressBack();
    }
}
