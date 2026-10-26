using Newmoonhana.Runtime.Tools.PopupUI.Window;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace GalaxyIO.Tools.PopupUI.Window
{
    /// <summary>
    /// 조작법 창(접은 상태). 열기 버튼만 가짐.
    /// </summary>
    public class WindowControlCollapse : WindowBase
    {
        [SerializeField] private Button show_btn;

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
            show_btn.onClick.AddListener(OnPressShow);
        }

        protected override void OnDisable()
        {
            base.OnDisable();
            show_btn.onClick.RemoveListener(OnPressShow);
        }

        public void AddListener(UnityAction<string> _action)
        {
            _changeState.AddListener(_action);
        }

        private void OnPressShow()
        {
            if (!IsActive()) return;
            _changeState?.Invoke("expand");
        }
    }
}
