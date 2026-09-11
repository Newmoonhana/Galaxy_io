using GalaxyIO.Tools.PopupUI.Window;
using Newmoonhana.Runtime.Tools.Scene;
using UnityEngine;
using UnityEngine.EventSystems;

namespace GalaxyIO.Tools.PopupUI.Popup
{
    /// <summary>
    /// 로비 팝업의 윈도우를 관리
    /// </summary>
    public class PopupLobbyController : UIBehaviour
    {
        private WindowProgressLobby progressWindow;
        private WindowSignIn signInWindow;
        private WindowSignUp signUpWindow;

        protected override void Awake()
        {
            base.Awake();

            progressWindow = transform.GetComponentInChildren<WindowProgressLobby>();
            signInWindow = transform.GetComponentInChildren<WindowSignIn>();
            signUpWindow = transform.GetComponentInChildren<WindowSignUp>();
            
            SceneStateManager.LoadSceneInst("S_Game", 1000);
        }
    }
}