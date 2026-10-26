using Newmoonhana.Runtime.Tools.PopupUI.Window;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace GalaxyIO.Tools.PopupUI.Window
{
    /// <summary>
    /// 조작법 창(펼친 상태). 숨기기 / 닫기 버튼을 가짐.
    /// </summary>
    public class WindowControlExpand : WindowBase
    {
        [SerializeField] private Button hide_btn;
        [SerializeField] private Button close_btn;

        private UnityEvent<string> _changeState;

        protected override void Awake()
        {
            base.Awake();
            _changeState = new UnityEvent<string>();
        }

        public override void Dispose()
        {
            base.Dispose();
            _changeState?.RemoveAllListeners();
            _changeState = null;
        }

        protected override void OnEnable()
        {
            base.OnEnable();
            hide_btn.onClick.AddListener(OnPressHide);
            close_btn.onClick.AddListener(OnPressClose);
        }

        protected override void OnDisable()
        {
            base.OnDisable();
            hide_btn.onClick.RemoveListener(OnPressHide);
            close_btn.onClick.RemoveListener(OnPressClose);
        }

        public void AddListener(UnityAction<string> _action)
        {
            _changeState.AddListener(_action);
        }

        private void OnPressHide()
        {
            if (!IsActive()) return;
            _changeState?.Invoke("collapse");
        }

        private void OnPressClose()
        {
            if (!IsActive()) return;
            _changeState?.Invoke("close");
        }
    }
}
