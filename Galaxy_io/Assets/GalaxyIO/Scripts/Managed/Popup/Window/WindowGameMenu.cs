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
        [SerializeField] private Button option_btn;
        [SerializeField] private Button quit_btn;
        [Tooltip("조작법 표기. 인게임 조작법 팝업이 닫힌 뒤에만 보임")]
        [SerializeField] private GameObject controlGuide_obj;

        private UnityEvent _resume;
        private UnityEvent _option;
        private UnityEvent _quit;

        protected override void Awake()
        {
            base.Awake();
            _resume = new UnityEvent();
            _option = new UnityEvent();
            _quit = new UnityEvent();

            //인게임 팝업이 살아 있으면 중복 표기가 되므로 끔
            controlGuide_obj.SetActive(Popup.ControlUI.IsClosed);
        }

        public override void Dispose()
        {
            base.Dispose();
            _resume?.RemoveAllListeners();
            _resume = null;
            _option?.RemoveAllListeners();
            _option = null;
            _quit?.RemoveAllListeners();
            _quit = null;
        }

        protected override void OnEnable()
        {
            base.OnEnable();
            resume_btn.onClick.AddListener(OnResumeBtnPress);
            option_btn.onClick.AddListener(OnOptionBtnPress);
            quit_btn.onClick.AddListener(OnQuitBtnPress);
        }

        protected override void OnDisable()
        {
            base.OnDisable();
            resume_btn.onClick.RemoveListener(OnResumeBtnPress);
            option_btn.onClick.RemoveListener(OnOptionBtnPress);
            quit_btn.onClick.RemoveListener(OnQuitBtnPress);
        }

        public void AddListener(UnityAction _onResume, UnityAction _onOption, UnityAction _onQuit)
        {
            _resume.AddListener(_onResume);
            _option.AddListener(_onOption);
            _quit.AddListener(_onQuit);
        }

        private void OnResumeBtnPress()
        {
            if (!IsActive()) return;
            _resume?.Invoke();
        }

        private void OnOptionBtnPress()
        {
            if (!IsActive()) return;
            _option?.Invoke();
        }

        private void OnQuitBtnPress()
        {
            if (!IsActive()) return;
            _quit?.Invoke();
        }
    }
}
