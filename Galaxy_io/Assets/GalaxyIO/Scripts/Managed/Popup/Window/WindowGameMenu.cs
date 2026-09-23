using Newmoonhana.Runtime.Tools.PopupUI.Window;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace GalaxyIO.Tools.PopupUI.Window
{
    /// <summary>
    /// 인게임 메뉴 창. 게임 재개/나가기.
    /// </summary>
    public class WindowGameMenu : WindowBase
    {
        [SerializeField] private Button resume_btn;
        [SerializeField] private Button quit_btn;

        private UnityEvent _resume;
        private UnityEvent _quit;

        protected override void Awake()
        {
            base.Awake();
            _resume = new UnityEvent();
            _quit = new UnityEvent();
        }

        public override void Dispose()
        {
            base.Dispose();
            _resume?.RemoveAllListeners();
            _resume = null;
            _quit?.RemoveAllListeners();
            _quit = null;
        }

        protected override void OnEnable()
        {
            base.OnEnable();
            resume_btn.onClick.AddListener(OnResumeBtnPress);
            quit_btn.onClick.AddListener(OnQuitBtnPress);
        }

        protected override void OnDisable()
        {
            base.OnDisable();
            resume_btn.onClick.RemoveListener(OnResumeBtnPress);
            quit_btn.onClick.RemoveListener(OnQuitBtnPress);
        }

        public void AddListener(UnityAction _onResume, UnityAction _onQuit)
        {
            _resume.AddListener(_onResume);
            _quit.AddListener(_onQuit);
        }

        private void OnResumeBtnPress()
        {
            if (!IsActive()) return;
            _resume?.Invoke();
        }

        private void OnQuitBtnPress()
        {
            if (!IsActive()) return;
            _quit?.Invoke();
        }
    }
}
