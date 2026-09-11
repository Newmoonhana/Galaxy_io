using GalaxyIO.Tools.PopupUI.Window;
using Newmoonhana.Runtime.Tools.PopupUI.Popup;
using UnityEngine;
using UnityEngine.EventSystems;

namespace GalaxyIO.Tools.PopupUI.Popup
{
    /// <summary>
    /// 로비 팝업의 윈도우를 관리
    /// </summary>
    public class PopupLobby : UIBehaviour, IPopup
    {
        private WindowProgressLobby progress_win;
        private WindowSignIn signIn_win;
        private WindowSignUp signUp_win;

        protected override void Awake()
        {
            base.Awake();

            progress_win = transform.GetComponentInChildren<WindowProgressLobby>();
            signIn_win = transform.GetComponentInChildren<WindowSignIn>();
            signUp_win = transform.GetComponentInChildren<WindowSignUp>();
        }
    }
}