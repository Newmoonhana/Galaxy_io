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
    /// 로비 UI의 윈도우를 관리
    /// </summary>
    public class LobbyUI : PopupBase
    {
        #region IState
        private class ProgressState : IState
        {
            WindowProgressLobby win;
            public ProgressState(WindowProgressLobby _win, UnityAction<string> _changeWindow, UnityAction _onLoginSuccess)
            {
                win = _win;
                win.AddListener(_changeWindow);
                win.AddListener(_onLoginSuccess);
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
            public SignInState(WindowSignIn _win, UnityAction<string> _changeWindow, UnityAction _onLoginSuccess)
            {
                win = _win;
                win.AddListener(_changeWindow);
                win.AddListener(_onLoginSuccess);
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
        /// <summary>
        /// 로그인 성공 후 게임 시작(서버 접속) 대기 창
        /// </summary>
        private class GameStartState : IState
        {
            private WindowGameStart win;
            public GameStartState(WindowGameStart _win, UnityAction _onConnected)
            {
                win = _win;
                win.AddListener(_onConnected);
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
        }

        protected override void Start()
        {
            base.Start();

            stateM.Add("progress", new ProgressState(transform.GetComponentInChildren<WindowProgressLobby>(true), ChangeState, () => ChangeState("gameStart")));
            stateM.Add("signIn", new SignInState(transform.GetComponentInChildren<WindowSignIn>(true), ChangeState, () => GameStart()));
            stateM.Add("signUp", new SignUpState(transform.GetComponentInChildren<WindowSignUp>(true), ChangeState));
            stateM.Add("gameStart", new GameStartState(transform.GetComponentInChildren<WindowGameStart>(true), () => ChangeState("none")));
            stateM.Add("none", new EmptyState());
            stateM.Change("progress");
        }

        public override void Dispose()
        {
            base.Dispose();
            stateM?.Dispose();
        }

        private void GameStart()
        {
            
        }
    }
}
