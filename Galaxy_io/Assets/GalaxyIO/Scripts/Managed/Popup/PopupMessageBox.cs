using GalaxyIO.Tools.PopupUI.Window;
using Newmoonhana.Runtime.Tools.PopupUI.Popup;
using UnityEngine;
using UnityEngine.EventSystems;

namespace GalaxyIO.Tools.PopupUI.Popup
{
    public class PopupMessageBox : UIBehaviour, IPopup
    {
        private WindowMsgBox msgBox_win;
        
        protected override void Awake()
        {
            base.Awake();

            msgBox_win = transform.GetComponentInChildren<WindowMsgBox>();
        }
    }
}
