using GalaxyIO.DOTS.Client.Components.Message;
using GalaxyIO.Tools.PopupUI.Window;
using JWServer.Runtime.Login;
using Newmoonhana.Runtime;
using Newmoonhana.Runtime.Tools.PopupUI.Popup;
using Newmoonhana.Runtime.Utils.DEBUG;
using Newmoonhana.Runtime.Utils.Pattern;
using Unity.Entities;
using Unity.NetCode;
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
        /// <summary>
        /// 우주선이 파괴된 동안 띄우는 리스폰 창
        /// </summary>
        private class RespawnState : IState
        {
            private WindowRespawn win;
            public RespawnState(WindowRespawn _win)
            {
                win = _win;
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

        // 파괴된 동안(=리스폰 대기)인지 판별하는 쿼리. 창 전환은 상태 전이 시에만 수행.
        private EntityQuery deathQuery;
        private bool hasDeathQuery;
        private bool wasDead;

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
            stateM.Add("respawn", new RespawnState(transform.GetComponentInChildren<WindowRespawn>(true)));
            stateM.Add("none", new EmptyState());
            stateM.Change("progress");

            World clientWorld = ClientServerBootstrap.ClientWorld;
            if (clientWorld != null)
            {
                deathQuery = clientWorld.EntityManager.CreateEntityQuery(
                    ComponentType.ReadOnly<PlayerDeathTag>()
                );
                hasDeathQuery = true;
            }
        }

        private void Update()
        {
            if (!hasDeathQuery) return;

            bool dead = !deathQuery.IsEmpty;
            if (dead == wasDead) return;

            wasDead = dead;
            ChangeState(dead ? "respawn" : "none");
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
