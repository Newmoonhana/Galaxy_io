using GalaxyIO.Tools.PopupUI.Window;
using Newmoonhana.Runtime.Tools.PopupUI.Popup;
using Newmoonhana.Runtime.Utils;
using Newmoonhana.Runtime.Utils.DEBUG;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;

namespace GalaxyIO.Tools.PopupUI.Popup
{
    public class CommonMessageBox : PopupBase
    {
        private const string key = "Common_MsgBox";

        private WindowMsgBox msgBox_win;

        /// <summary>
        /// 안내창 생성. 부모를 주지 않으면 자체 Canvas 로 뜸.
        /// </summary>
        public static async Awaitable<CommonMessageBox> Open(Transform _parent = null)
        {
            await AddressableAssetsManager.gameObject_lst.LoadData(key);
            GameObject winObj = Instantiate(AddressableAssetsManager.gameObject_lst.GetData(key).Data, _parent);
            AddressableAssetsManager.gameObject_lst.GetData(key).UnLoad();

            CommonMessageBox msgBox = winObj.GetComponent<CommonMessageBox>();
            if (msgBox == null) DebugLog.LogError($"프리팹 {key} 등록 안됨!");

            return msgBox;
        }
        
        protected override void Awake()
        {
            base.Awake();

            msgBox_win = transform.GetComponentInChildren<WindowMsgBox>();
        }

        protected override void Start()
        {
            base.Start();
            msgBox_win.AddListener(Close);
        }

        /// <summary>ESC. 닫기 버튼과 동일.</summary>
        public override void TryCancel()
        {
            if (isClosing) return;
            msgBox_win.Cancel();
        }

        protected override void OnDestroy()
        {
            base.OnDestroy();
            msgBox_win?.Dispose();
            msgBox_win = null;
        }

        public void SetWinText(string _label, string _responseMessage)
        {
            msgBox_win.SetTitleText(_label);
            msgBox_win.SetMessageText(_responseMessage);
        }

        /// <summary>
        /// 닫기 버튼에 동작 추가. 창은 항상 닫힘.
        /// </summary>
        public void AddListener(UnityAction _action)
        {
            msgBox_win.AddListener(_action);
        }
        
        public void Refresh()
        {
            msgBox_win.Refresh();
        }
    }
}
