using Newmoonhana.Runtime.Tools.PopupUI.Window;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace GalaxyIO.Tools.PopupUI.Window
{
    public class WindowMsgBox : WindowBase
    {
        [SerializeField] private TMP_Text label_txt;
        [SerializeField] private TMP_Text msg_txt;
        [SerializeField] private Button exit_btn;
        
        private UnityEvent closeMsgBox;
        
        protected override void Awake()
        {
            base.Awake();
            closeMsgBox = new UnityEvent();
        }

        public override void Dispose()
        {
            base.Dispose();
            closeMsgBox?.RemoveAllListeners();
            closeMsgBox = null;
        }

        protected override void OnEnable()
        {
            base.OnEnable();
            
            exit_btn.onClick.AddListener(OnExitBtnPress);
        }

        protected override void OnDisable()
        {
            base.OnDisable();
            
            exit_btn.onClick.RemoveListener(OnExitBtnPress);
        }
        
        public void AddListener(UnityAction _action)
        {
            closeMsgBox.AddListener(_action);
        }

        public void SetTitleText(string title)
        {
            label_txt.SetText(title);
            
        }

        public void SetMessageText(string message)
        {
            msg_txt.SetText(message);
            
        }

        public void Refresh()
        {
            label_txt.ForceMeshUpdate();
            msg_txt.ForceMeshUpdate();
        }

        public void OnExitBtnPress()
        {
            closeMsgBox?.Invoke();
        }

        /// <summary>닫기 버튼과 같은 처리. ESC 용.</summary>
        public void Cancel() => OnExitBtnPress();
    }

}