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
        //ESC 처리를 가진 상태. 구현하지 않은 상태는 ESC 무반응
        private interface ICancelState
        {
            void Cancel();
        }

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
        private class SignInState : IState, ICancelState
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

            public void Cancel() => win.Cancel();
        }
        private class SignUpState : IState, ICancelState
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

            public void Cancel() => win.Cancel();
        }
        /// <summary>
        /// 통합 ID로 이 게임의 계정을 추가하는 창
        /// </summary>
        private class SignUpJWServerState : IState, ICancelState
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

            public void Cancel() => win.Cancel();
        }
        /// <summary>
        /// 공개/사설 서버를 고르는 분기 창
        /// </summary>
        private class ServerBranchState : IState, ICancelState
        {
            private WindowServerBranch win;

            public ServerBranchState(WindowServerBranch _win, UnityAction<string> _changeWindow)
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

            public void Cancel() => win.Cancel();
        }
        /// <summary>
        /// 게임 시작(서버 접속) 창. 공개/사설 창이 같은 형태라 하나로 씀.
        /// </summary>
        private class GameStartState : IState, ICancelState
        {
            private WindowGameStartBase win;

            public GameStartState(WindowGameStartBase _win, UnityAction<string> _changeWindow)
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

            public void Cancel() => win.Cancel();
        }
        /// <summary>
        /// 사설 서버를 직접 여는 창
        /// </summary>
        private class ServerHostState : IState, ICancelState
        {
            private WindowServerHost win;

            public ServerHostState(WindowServerHost _win, UnityAction<string> _changeWindow)
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

            public void Cancel() => win.Cancel();
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
            stateM.Add("serverBranch", new ServerBranchState(transform.GetComponentInChildren<WindowServerBranch>(true), ChangeState));
            stateM.Add("gameStartPublic", new GameStartState(transform.GetComponentInChildren<WindowGameStartPublic>(true), ChangeState));
            stateM.Add("gameStartPrivate", new GameStartState(transform.GetComponentInChildren<WindowGameStartPrivate>(true), ChangeState));
            stateM.Add("serverHost", new ServerHostState(transform.GetComponentInChildren<WindowServerHost>(true), ChangeState));
            stateM.Add("none", new EmptyState());
            stateM.Change("progress");
            _ = RunAutoLogIn();
        }

        /// <summary>
        /// 인게임에서 돌아오면 로비 씬 루트가 다시 켜짐.
        /// 남은 안내가 있으면 떠날 때 창 위에 띄우고, 없으면 로비 메인으로 되돌림.
        /// </summary>
        protected override void OnEnable()
        {
            base.OnEnable();

            // 최초 생성 시엔 상태가 아직 없음. 복귀한 경우만 처리함.
            if (stateM == null || string.IsNullOrEmpty(stateM.CurrentToString)) return;

            // 로비가 다시 보이므로 세션용 로딩 오버레이는 필요 없음.
            CommonProgress.HideAcrossScenes();

            if (LobbyReturn.TakeNotice(out string title, out string body))
            {
                _ = ShowNotice(title, body);
                return;
            }

            ChangeState("main");
        }

        public override void Dispose()
        {
            base.Dispose();
            stateM?.Dispose();
        }

        /// <summary>
        /// ESC. 현재 창의 나가기 버튼과 같은 곳으로 보냄. 갈 곳이 없는 창은 무반응.
        /// </summary>
        public override void TryCancel()
        {
            if (isClosing) return;

            if (stateM?.Current is ICancelState cancelable) cancelable.Cancel();
        }

        /// <summary>
        /// 복귀 안내를 로비 화면 위에 띄움.
        /// </summary>
        private async Awaitable ShowNotice(string _title, string _body)
        {
            CommonMessageBox msgBox = await CommonMessageBox.Open(transform);
            if (msgBox == null) return;

            msgBox.SetWinText(_title, _body);
            msgBox.Refresh();
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