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
            label_txt.ForceMeshUpdate();
        }

        public void SetMessageText(string message)
        {
            TMP_Text textMeshPro = msg_txt.GetComponent<TMP_Text>();
            textMeshPro.SetText(message);
            textMeshPro.ForceMeshUpdate();
        }

        public void OnExitBtnPress()
        {
            closePopup?.Invoke();
        }
    }

}