using GalaxyIO.Tools.PopupUI.Window;
using Newmoonhana.Runtime.Tools.PopupUI.Popup;
using UnityEngine.Events;

namespace GalaxyIO.Tools.PopupUI.Popup
{
    /// <summary>
    /// ESC로 여는 인게임 메뉴 팝업.
    /// </summary>
    public class GameMenuUI : PopupBase
    {
        private WindowGameMenu menu_win;

        protected override void Awake()
        {
            base.Awake();
            menu_win = transform.GetComponentInChildren<WindowGameMenu>();
        }

        protected override void OnDestroy()
        {
            base.OnDestroy();
            menu_win?.Dispose();
            menu_win = null;
        }

        /// <summary>
        /// 생성 직후 호출. 재개/나가기 처리는 생성한 쪽이 담당.
        /// </summary>
        public void AddListener(UnityAction _onResume, UnityAction _onQuit)
        {
            menu_win.AddListener(_onResume, _onQuit);
        }
    }
}
