using GalaxyIO.Tools.PopupUI.Window;
using Newmoonhana.Runtime.Tools.PopupUI.Popup;
using Newmoonhana.Runtime.Utils.Pattern;

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

        protected override void Start()
        {
            base.Start();

            stateM.Add("respawn", new RespawnState(transform.GetComponentInChildren<WindowRespawn>(true)));
            stateM.Change("respawn");
        }
    }
}
