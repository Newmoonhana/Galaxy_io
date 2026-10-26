using GalaxyIO.Tools.PopupUI.Window;
using Newmoonhana.Runtime.Tools.PopupUI.Popup;
using Newmoonhana.Runtime.Utils;
using Newmoonhana.Runtime.Utils.Pattern;
using UnityEngine;
using UnityEngine.Events;

namespace GalaxyIO.Tools.PopupUI.Popup
{
    /// <summary>
    /// 인게임 조작법 팝업. 펼침 / 접힘 / 닫힘 상태를 가짐.
    /// </summary>
    public class ControlUI : PopupBase
    {
        private const string key = "Control_UI";

        #region IState
        /// <summary>
        /// 창 보임. 숨기기 / 닫기로 전이함.
        /// </summary>
        private class ExpandState : IState
        {
            private WindowControlExpand win;
            public ExpandState(WindowControlExpand _win, UnityAction<string> _changeState)
            {
                win = _win;
                win.AddListener(_changeState);
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
        /// 창 숨김. 열기 버튼만 남김.
        /// </summary>
        private class CollapseState : IState
        {
            private WindowControlCollapse win;
            public CollapseState(WindowControlCollapse _win, UnityAction<string> _changeState)
            {
                win = _win;
                win.AddListener(_changeState);
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
        /// 씬에서 팝업을 지움. 다시 볼 경로는 게임 메뉴의 같은 표기뿐.
        /// </summary>
        private class CloseState : IState
        {
            private ControlUI popup;
            public CloseState(ControlUI _popup) { popup = _popup; }

            public void UpdateState(float elapsedTime) {}
            public void Render() {}
            public void OnEnter(object p_data)
            {
                //퇴장 연출 중에도 게임 메뉴 쪽 표기가 켜져야 하므로 먼저 세움
                isClosed = true;
                popup.Close();
            }
            public void OnExit() {}
            public void Dispose() { popup = null; }
        }
        #endregion

        // 떠 있는 팝업. 로딩 완료 통지를 받을 대상
        private static ControlUI opened;
        // close 상태를 지났는지. 게임 메뉴의 조작법 표기 조건
        private static bool isClosed;

        public static ControlUI Opened => opened;
        /// <summary>인게임 조작법 팝업이 닫혔는지. 게임 메뉴 쪽 표기 조건.</summary>
        public static bool IsClosed => isClosed;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStaticData()
        {
            opened = null;
            isClosed = false;
        }

        /// <summary>
        /// 조작법 팝업 생성. 자체 Canvas가 있어 부모 불필요.
        /// 로딩 중에는 none 상태로 떠 있다가 <see cref="OnLoadComplete"/>로 접힌 창이 뜸.
        /// </summary>
        public static async Awaitable Open()
        {
            if (opened != null) return;

            await AddressableAssetsManager.gameObject_lst.LoadData(key);

            // 로드를 기다리는 동안 다른 호출이 먼저 띄웠을 수 있음.
            if (opened != null) return;

            Instantiate(AddressableAssetsManager.gameObject_lst.GetData(key).Data);
        }

        /// <summary>
        /// 프리팹 참조 해제. 인게임 씬을 떠날 때 <see cref="Main.GalaxyIO_Main"/>이 부름.
        /// </summary>
        public static void Unload()
        {
            AddressableAssetsManager.gameObject_lst.RemoveData(key);
        }

        protected override void Awake()
        {
            base.Awake();
            opened = this;
            isClosed = false;
        }

        protected override void Start()
        {
            base.Start();

            stateM.Add("none", new EmptyState());
            stateM.Add("expand", new ExpandState(
                transform.GetComponentInChildren<WindowControlExpand>(true), ChangeState));
            stateM.Add("collapse", new CollapseState(
                transform.GetComponentInChildren<WindowControlCollapse>(true), ChangeState));
            stateM.Add("close", new CloseState(this));
            stateM.Change("none");
        }

        protected override void OnDestroy()
        {
            base.OnDestroy();
            if (opened == this) opened = null;
        }

        /// <summary>
        /// 로딩(= Common_Progress) 종료 통지. 최초 한 번만 창을 띄움.
        /// 인게임 시야를 가리지 않게 접힌 상태로 시작함. 펼치기는 열기 버튼으로.
        /// </summary>
        public void OnLoadComplete()
        {
            if (stateM == null) return;
            if (stateM.CurrentToString != "none") return;

            ChangeState("collapse");
        }
    }
}
