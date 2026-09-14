using Newmoonhana.Runtime.Tools.PopupUI.Window;
using UnityEngine;
using UnityEngine.Events;

namespace GalaxyIO.Tools.PopupUI.Window
{
    public class WindowProgressLobby : WindowBase
    {
        // [TEMP] 해당 윈도우 디자인 커스텀 시 사용 가능성 높은 커스텀 윈도우 스크립트

        private UnityEvent<string> _changeWindow;

        protected override void Awake()
        {
            base.Awake();
            _changeWindow = new UnityEvent<string>();
        }

        public override void Dispose()
        {
            base.Dispose();
            _changeWindow?.RemoveAllListeners();
            _changeWindow = null;
        }

        public override void Open()
        {
            base.Open();
            _ = RunProgress();
        }

        public void AddListener(UnityAction<string> _action)
        {
            _changeWindow.AddListener(_action);
        }

        private async Awaitable RunProgress()
        {
            // TODO -> 대기시간 서버 불러옴 여부로 체크?
            await Awaitable.WaitForSecondsAsync(1f);
            _changeWindow?.Invoke("signIn");
        }
    }
}