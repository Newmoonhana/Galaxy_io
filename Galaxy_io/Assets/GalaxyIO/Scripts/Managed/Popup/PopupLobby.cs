using GalaxyIO.Tools.PopupUI.Window;
using JWServer.Runtime.Login;
using Newmoonhana.Runtime;
using Newmoonhana.Runtime.Tools.PopupUI.Popup;
using Newmoonhana.Runtime.Utils.DEBUG;
using Newmoonhana.Runtime.Utils.Pattern;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;

namespace GalaxyIO.Tools.PopupUI.Popup
{
    /// <summary>
    /// 로비 팝업의 윈도우를 관리
    /// </summary>
    public class PopupLobby : PopupBase
    {
        #region IState
        private class ProgressState : IState
        {
            WindowProgressLobby win;
            public ProgressState(WindowProgressLobby _win, UnityAction<string> _changeWindow)
            {
                win = _win;
                win.AddListener(_changeWindow);
                OnExit();
            }
            
            public void UpdateState(float elapsedTime) {}
            public void Render() {}
            public void OnEnter(object p_data)
            {
                win.Open();
                win.Activate();
            }
            public void OnExit()
            {
                win.Deactivate();
                win.Close();
            }

            public void Dispose()
            {
                win?.Dispose();
                win = null;
            }
        }
        private class SignInState : IState
        {
            private WindowSignIn win;
            public SignInState(WindowSignIn _win, UnityAction<string> _changeWindow)
            {
                win = _win;
                win.AddListener(_changeWindow);
                OnExit();
            }
            
            public void UpdateState(float elapsedTime) {}
            public void Render() {}
            public void OnEnter(object p_data)
            {
                win.Open();
                win.Activate();
            }
            public void OnExit()
            {
                win.Deactivate();
                win.Close();
            }
            public void Dispose()
            {
                win?.Dispose();
                win = null;
            }
        }
        private class SignUpState : IState
        {
            private WindowSignUp win;
            public SignUpState(WindowSignUp _win, UnityAction<string> _changeWindow)
            {
                win = _win;
                win.AddListener(_changeWindow);
                OnExit();
            }
            
            public void UpdateState(float elapsedTime) {}
            public void Render() {}
            public void OnEnter(object p_data)
            {
                win.Open();
                win.Activate();
            }
            public void OnExit()
            {
                win.Deactivate();
                win.Close();
            }
            public void Dispose()
            {
                win?.Dispose();
                win = null;
            }
        }
        #endregion

        protected override void Awake()
        {
            base.Awake();
            
            LoginManager.ResetOnline();
            stateM.Add("progress", new ProgressState(transform.GetComponentInChildren<WindowProgressLobby>(), ChangeState));
            stateM.Add("signIn", new SignInState(transform.GetComponentInChildren<WindowSignIn>(), ChangeState));
            stateM.Add("signUp", new SignUpState(transform.GetComponentInChildren<WindowSignUp>(), ChangeState));
        }

        protected override void Start()
        {
            base.Start();
            
            stateM.Change("progress");
        }

        public override void Dispose()
        {
            base.Dispose();
            stateM?.Dispose();
        }
    }
}