using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace GalaxyIO.Tools.PopupUI.Window
{
    public class WindowMsgBox : UIBehaviour
    {
        [SerializeField] private TMP_Text label_txt;
        [SerializeField] private TMP_Text msg_txt;
        [SerializeField] private Button exit_btn;

        protected override void OnEnable()
        {
            base.OnEnable();
            
            exit_btn.onClick.AddListener(OnExitPressed);
        }

        protected override void OnDisable()
        {
            base.OnDisable();
            
            exit_btn.onClick.RemoveListener(OnExitPressed);
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

        public void OnExitPressed()
        {
            // TODO -> 팝업 상태 변경
        }
    }

}