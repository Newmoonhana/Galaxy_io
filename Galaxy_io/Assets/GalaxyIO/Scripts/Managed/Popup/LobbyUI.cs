using GalaxyIO.Tools.PopupUI.Window;
using JWServer.Runtime.DTO;
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
        /// <summary>
        /// 로비 메인 메뉴 창
        /// </summary>
        private class MainState : IState
        {
            private WindowLobbyMain win;
            public MainState(WindowLobbyMain _win, UnityAction<string> _changeWindow)
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
            public SignUpState(WindowSignUp _win, UnityAction<string> _changeWindow,
                UnityAction<GameUserRequestDTO> _toJWServerSignUp)
            {
                win = _win;
                win.AddListener(_changeWindow);
                win.AddJWServerListener(_toJWServerSignUp);
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
        /// 통합 ID로 이 게임의 계정을 추가하는 창
        /// </summary>
        private class SignUpJWServerState : IState
        {
            private WindowSignUpJWServer win;
            public SignUpJWServerState(WindowSignUpJWServer _win, UnityAction<string> _changeWindow)
            {
                win = _win;
                win.AddListener(_changeWindow);
                OnExit();
            }

            public void UpdateState(float elapsedTime) {}
            public void Render() {}
            public void OnEnter(object p_data)
            {
                if (p_data is GameUserRequestDTO userData)
                    win.SetUserData(userData);
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
        /// 게임 시작(서버 접속) 창
        /// </summary>
        private class GameStartState : IState
        {
            private WindowGameStart win;
            public GameStartState(WindowGameStart _win, UnityAction<string> _changeWindow, UnityAction _onConnected)
            {
                win = _win;
                win.AddListener(_changeWindow);
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

            stateM.Add("progress", new EmptyState());
            stateM.Add("main", new MainState(transform.GetComponentInChildren<WindowLobbyMain>(true), ChangeState));
            stateM.Add("signIn", new SignInState(transform.GetComponentInChildren<WindowSignIn>(true), ChangeState, () => ChangeState("main")));
            stateM.Add("signUp", new SignUpState(transform.GetComponentInChildren<WindowSignUp>(true), ChangeState,
                userData => stateM.Change("signUpJWServer", userData)));
            stateM.Add("signUpJWServer", new SignUpJWServerState(transform.GetComponentInChildren<WindowSignUpJWServer>(true), ChangeState));
            stateM.Add("gameStart", new GameStartState(transform.GetComponentInChildren<WindowGameStart>(true), ChangeState, () => ChangeState("none")));
            stateM.Add("none", new EmptyState());
            stateM.Change("progress");
            _ = RunAutoLogIn();
        }

        public override void Dispose()
        {
            base.Dispose();
            stateM?.Dispose();
        }

        /// <summary>
        /// 진행 팝업을 띄운 채로 자동 로그인. 결과와 무관하게 메인으로 이동.
        /// </summary>
        private async Awaitable RunAutoLogIn()
        {
            await CommonProgress.Show<GameUserResponseDTO>(transform, async () =>
            {
                await Awaitable.WaitForSecondsAsync(0.5f);  //너무 빨라서 인지 안되서 넣음
                return await LoginManager.AutoLogIn();
            });

            if (stateM == null) return;  // 대기 중 삭제됨
            ChangeState("main");
        }
    }
}