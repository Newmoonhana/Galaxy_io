using GalaxyIO.Tools.PopupUI.Window;
using Newmoonhana.Runtime.Tools.PopupUI.Popup;
using Newmoonhana.Runtime.Utils;
using UnityEngine;

namespace GalaxyIO.Tools.PopupUI.Popup
{
    /// <summary>
    /// 크레딧 팝업. 로비 밖(웹 리스폰 창)에서 크레딧을 띄울 때 씀.
    /// </summary>
    public class CommonCredit : PopupBase
    {
        private const string key = "Common_Credit";

        private WindowCredit credit_win;

        // 떠 있는 크레딧 팝업. 연타로 여러 장 뜨지 않게 함
        private static CommonCredit opened;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStaticData() => opened = null;

        /// <summary>
        /// 크레딧 팝업 생성. 자체 Canvas가 있어 부모 불필요.
        /// </summary>
        public static async Awaitable Open()
        {
            // 이미 떠 있으면 무시.
            if (opened != null) return;

            await AddressableAssetsManager.gameObject_lst.LoadData(key);

            // 로드를 기다리는 동안 다른 호출이 먼저 띄웠을 수 있음.
            if (opened != null) return;

            Instantiate(AddressableAssetsManager.gameObject_lst.GetData(key).Data);
        }

        protected override void Awake()
        {
            base.Awake();
            opened = this;
            credit_win = transform.GetComponentInChildren<WindowCredit>();
        }

        protected override void Start()
        {
            base.Start();
            // 로비에선 메인 창으로 돌아가는 버튼. 팝업에선 닫기.
            credit_win.AddListener(_ => Close());
        }

        /// <summary>ESC. 나가기 버튼과 동일.</summary>
        public override void TryCancel()
        {
            if (isClosing) return;
            credit_win.Cancel();
        }

        protected override void OnDestroy()
        {
            base.OnDestroy();
            if (opened == this) opened = null;
            credit_win?.Dispose();
            credit_win = null;
        }
    }
}
