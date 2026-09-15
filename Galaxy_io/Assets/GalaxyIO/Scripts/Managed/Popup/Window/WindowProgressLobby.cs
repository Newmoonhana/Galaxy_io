using JWServer.Runtime.DTO;
using JWServer.Runtime.Login;
using JWServer.Runtime.Utils;
using Newmoonhana.Runtime.Tools.PopupUI.Window;
using Newmoonhana.Runtime.Utils.DEBUG;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.SceneManagement;

namespace GalaxyIO.Tools.PopupUI.Window
{
    public class WindowProgressLobby : WindowBase
    {
        // [TEMP] 해당 윈도우 디자인 커스텀 시 사용 가능성 높은 커스텀 윈도우 스크립트

        private UnityEvent<string> _changeWindow;
        private Awaitable runProgress_handle;

        protected override void Awake()
        {
            base.Awake();
            _changeWindow = new UnityEvent<string>();
        }

        protected override void Start()
        {
            base.Start();
            runProgress_handle = RunProgress();
        }

        public override void Dispose()
        {
            base.Dispose();
            runProgress_handle?.Cancel();
            runProgress_handle = null;
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
            await Awaitable.WaitForSecondsAsync(0.5f);  //너무 빨라서 인지 안되서 넣음
            
            GameUserResponseDTO result = await LoginManager.AutoLogIn();
            
            if (ResponseMsgUtil.IsOk(result.responseMessage) || result.responseMessage is CommonMsg.ALREADY_ONLINE)
            {
                SceneManager.LoadScene("GalaxyIO_Main", LoadSceneMode.Single);
                return;
            }
            
            await Awaitable.WaitForSecondsAsync(0.5f);  //너무 빨라서 인지 안되서 넣음
            _changeWindow?.Invoke("signIn");
        }
    }
}