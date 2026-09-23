using GalaxyIO.Tools.PopupUI.Window;
using Newmoonhana.Runtime.Tools.PopupUI.Popup;
using Newmoonhana.Runtime.Utils.Pattern;
using UnityEngine.Events;

namespace GalaxyIO.Tools.PopupUI.Popup
{
    /// <summary>
    /// 우주선 파괴 시 리스폰 윈도우를 관리
    /// </summary>
    public class RespawnUI : PopupBase
    {
        #region IState
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

        private WindowRespawn respawn_win;

        protected override void Awake()
        {
            base.Awake();
            respawn_win = transform.GetComponentInChildren<WindowRespawn>(true);
        }

        protected override void Start()
        {
            base.Start();

            stateM.Add("respawn", new RespawnState(respawn_win));
            stateM.Change("respawn");
        }

        /// <summary>
        /// 생성 직후 호출. 나가기 처리는 생성한 쪽이 담당.
        /// </summary>
        public void AddListener(UnityAction _onQuit)
        {
            respawn_win.AddListener(_onQuit);
        }
    }
}
