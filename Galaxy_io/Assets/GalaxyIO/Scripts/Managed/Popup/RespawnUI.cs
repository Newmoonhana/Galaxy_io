using GalaxyIO.DOTS.Authoring;
using GalaxyIO.DOTS.Components.Singleton;
using GalaxyIO.Tools.PopupUI.Window;
using Newmoonhana.Runtime.Tools.PopupUI.Popup;
using Newmoonhana.Runtime.Tools.PopupUI.Window;
using Newmoonhana.Runtime.Utils.Pattern;
using Unity.Entities;
using Unity.NetCode;
using UnityEngine.Events;

namespace GalaxyIO.Tools.PopupUI.Popup
{
    /// <summary>
    /// 우주선 파괴 시 리스폰 팝업. 모드별 리스폰 창과 사양 창을 관리함.
    /// </summary>
    public class RespawnUI : PopupBase
    {
        private const string stateRespawn = "respawn";
        private const string stateSelect = "select";
        private const string stateSpec = "spec";

        #region IState
        /// <summary>
        /// 모드별 리스폰 창. 사양 창이 위에 열리면 입력만 막고 남음.
        /// </summary>
        private class RespawnState : IState
        {
            private WindowBase win;
            public RespawnState(WindowBase _win)
            {
                win = _win;
                win.Deactivate();
                win.Close();
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
            }
            public void Dispose()
            {
                win?.Dispose();
                win = null;
            }
        }

        /// <summary>
        /// 사양 창. 팀 모드 창 위에 겹쳐 열림. 열 때 p_data 로 기종을 받음.
        /// </summary>
        private class SpecState : IState
        {
            private WindowBase select_win;
            private WindowSpaceshipSpec win;
            public SpecState(WindowBase _selectWin, WindowSpaceshipSpec _win)
            {
                select_win = _selectWin;
                win = _win;
                OnExit();
            }

            public void UpdateState(float elapsedTime) {}
            public void Render() {}
            public void OnEnter(object p_data)
            {
                select_win.Deactivate();

                win.Open();
                win.SetSpaceship((SpaceshipAsset)p_data, false);
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
                select_win = null;
            }
        }
        #endregion

        private WindowRespawn respawn_win;
        private WindowRespawnSelect select_win;
        private WindowSpaceshipSpec spec_win;

        protected override void Awake()
        {
            base.Awake();
            respawn_win = transform.GetComponentInChildren<WindowRespawn>(true);
            select_win = transform.GetComponentInChildren<WindowRespawnSelect>(true);
            spec_win = transform.GetComponentInChildren<WindowSpaceshipSpec>(true);
        }

        protected override void Start()
        {
            base.Start();

            select_win.OnSpecRequested += spaceship => stateM.Change(stateSpec, spaceship);
            spec_win.OnEquip += select_win.RequestStart;
            spec_win.OnClosed += OnSpecClosed;

            stateM.Add(stateRespawn, new RespawnState(respawn_win));
            stateM.Add(stateSelect, new RespawnState(select_win));
            stateM.Add(stateSpec, new SpecState(select_win, spec_win));
            stateM.Change(IsTeamMode() ? stateSelect : stateRespawn);
        }

        protected override void OnDestroy()
        {
            base.OnDestroy();

            respawn_win = null;
            select_win = null;
            spec_win = null;
        }

        /// <summary>
        /// 생성 직후 호출. 나가기 처리는 생성한 쪽이 담당.
        /// </summary>
        public void AddListener(UnityAction _onQuit)
        {
            respawn_win.AddListener(_onQuit);
            select_win.AddListener(_onQuit);
        }

        /// <summary>
        /// ESC. 사양 창이 열려 있으면 그것만 닫음. 리스폰 창은 닫지 않음.
        /// </summary>
        public override void TryCancel()
        {
            if (isClosing) return;

            if (stateM != null && stateM.CurrentToString == stateSpec) OnSpecClosed();
        }

        private void LateUpdate()
        {
            if (stateM != null && stateM.CurrentToString == stateSpec) spec_win.RefreshStart(select_win.CanStart);
        }

        // 모드는 씬 단위 고정. 모드 설정이 있으면 팀 모드.
        private static bool IsTeamMode()
        {
            World world = ClientServerBootstrap.ClientWorld;

            if (!ClientWorldScope.IsAlive(world)) return false;

            using EntityQuery modeQuery = world.EntityManager.CreateEntityQuery(ComponentType.ReadOnly<GameModeComponent>());
            return !modeQuery.IsEmpty;
        }

        private void OnSpecClosed() => stateM.Change(stateSelect);
    }
}
