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

        // 떠 있는 옵션 팝업. 연타로 여러 장 뜨지 않게 함
        private static CommonOption opened;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStaticData() => opened = null;

        /// <summary>
        /// 옵션 팝업 생성. 자체 Canvas가 있어 부모 불필요.
        /// </summary>
        public static async Awaitable Open()
        {
            // 이미 떠 있으면 무시.
            if (opened != null) return;

            // 언어 콤보박스가 AvailableLocales를 바로 읽으므로 초기화 먼저.
            await LocalizationSettings.InitializationOperation.Task;
            await AddressableAssetsManager.gameObject_lst.LoadData(key);

            // 로드를 기다리는 동안 다른 호출이 먼저 띄웠을 수 있음.
            if (opened != null)
            {
                AddressableAssetsManager.gameObject_lst.GetData(key).UnLoad();
                return;
            }

            Instantiate(AddressableAssetsManager.gameObject_lst.GetData(key).Data);
            AddressableAssetsManager.gameObject_lst.GetData(key).UnLoad();
        }

        protected override void Awake()
        {
            base.Awake();
            opened = this;
            option_win = transform.GetComponentInChildren<WindowOption>();
        }

        protected override void Start()
        {
            base.Start();
            option_win.AddListener(Close);
        }

        /// <summary>ESC. 나가기 버튼과 동일하게 변경값을 되돌림.</summary>
        public override void TryCancel()
        {
            if (isClosing) return;
            option_win.Cancel();
        }

        protected override void OnDestroy()
        {
            base.OnDestroy();
            if (opened == this) opened = null;
            option_win?.Dispose();
            option_win = null;
        }
    }
}
