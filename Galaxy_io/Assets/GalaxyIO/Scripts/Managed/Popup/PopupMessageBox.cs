using GalaxyIO.Tools.PopupUI.Window;
using Newmoonhana.Runtime.Tools.PopupUI.Popup;
using UnityEngine;
using UnityEngine.EventSystems;

namespace GalaxyIO.Tools.PopupUI.Popup
{
    public class PopupMessageBox : PopupBase
    {
        private WindowMsgBox msgBox_win;
        
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
        
        public void Refresh()
        {
            msgBox_win.Refresh();
        }

        public void Close()
        {
            Destroy(gameObject);    //자주 호출되는 윈도우가 아니라서 단순 삭제로 처리
        }
    }
}
