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
        
        private UnityEvent closePopup;
        
        protected override void Awake()
        {
            base.Awake();
            closePopup = new UnityEvent();
        }

        public override void Dispose()
        {
            base.Dispose();
            closePopup?.RemoveAllListeners();
            closePopup = null;
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
            closePopup.AddListener(_action);
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
            closePopup?.Invoke();
        }
    }

}