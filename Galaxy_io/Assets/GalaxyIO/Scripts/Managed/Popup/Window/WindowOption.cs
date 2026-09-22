using Newmoonhana.Runtime.Tools.PopupUI.Window;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace GalaxyIO.Tools.PopupUI.Window
{
    /// <summary>
    /// 옵션(환경설정) 창.
    /// </summary>
    public class WindowOption : WindowBase
    {
        [SerializeField] private Button exit_btn;

        private UnityEvent closeOption;

        protected override void Awake()
        {
            base.Awake();
            closeOption = new UnityEvent();
        }

        public override void Dispose()
        {
            base.Dispose();
            closeOption?.RemoveAllListeners();
            closeOption = null;
        }

        protected override void OnEnable()
        {
            base.OnEnable();
            exit_btn.onClick.AddListener(OnExitBtnPress);
        }

        protected override void OnDisable()
        {
            base.OnDisable();
            exit_btn.onClick.RemoveListener(OnExitBtnPress);
        }

        public void AddListener(UnityAction _action)
        {
            closeOption.AddListener(_action);
        }

        private void OnExitBtnPress()
        {
            if (!IsActive()) return;
            closeOption?.Invoke();
        }
    }
}
