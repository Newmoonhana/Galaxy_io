using GalaxyIO.Tools.PopupUI.Window;
using Newmoonhana.Runtime.Tools.PopupUI.Popup;
using Newmoonhana.Runtime.Utils;
using UnityEngine;
using UnityEngine.Localization.Settings;

namespace GalaxyIO.Tools.PopupUI.Popup
{
    /// <summary>
    /// 옵션(환경설정) 팝업. 로비/인게임 공용.
    /// </summary>
    public class CommonOption : PopupBase
    {
        private const string key = "Common_Option";

        private WindowOption option_win;

        /// <summary>
        /// 옵션 팝업 생성. 자체 Canvas가 있어 부모 불필요.
        /// </summary>
        public static async Awaitable Open()
        {
            // 언어 콤보박스가 AvailableLocales를 바로 읽으므로 초기화 먼저.
            await LocalizationSettings.InitializationOperation.Task;
            await AddressableAssetsManager.gameObject_lst.LoadData(key);
            Instantiate(AddressableAssetsManager.gameObject_lst.GetData(key).Data);
            AddressableAssetsManager.gameObject_lst.GetData(key).UnLoad();
        }

        protected override void Awake()
        {
            base.Awake();
            option_win = transform.GetComponentInChildren<WindowOption>();
        }

        protected override void Start()
        {
            base.Start();
            option_win.AddListener(Close);
        }

        protected override void OnDestroy()
        {
            base.OnDestroy();
            option_win?.Dispose();
            option_win = null;
        }
    }
}
