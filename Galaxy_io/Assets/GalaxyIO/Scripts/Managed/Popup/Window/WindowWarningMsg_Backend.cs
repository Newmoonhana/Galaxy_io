using TMPro;
using UnityEngine;

namespace GalaxyIO.Tools.PopupUI.Window
{
    public sealed class WindowWarningMsg_Backend : MonoBehaviour
    {
        [SerializeField]
        private GameObject titleText;

        [SerializeField]
        private GameObject messageText;

        [SerializeField]
        private GameObject selfScreen;

        public void SetTitleText(string title)
        {
            TMP_Text textMeshPro = titleText.GetComponent<TMP_Text>();
            textMeshPro.SetText(title);
            textMeshPro.ForceMeshUpdate();
        }

        public void SetMessageText(string message)
        {
            TMP_Text textMeshPro = messageText.GetComponent<TMP_Text>();
            textMeshPro.SetText(message);
            textMeshPro.ForceMeshUpdate();
        }

        public void OnExitPressed()
        {
            selfScreen.SetActive(false);
        }
    }

}