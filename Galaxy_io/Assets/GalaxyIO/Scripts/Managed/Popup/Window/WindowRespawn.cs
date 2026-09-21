using GalaxyIO.DOTS.Client.Networking;
using Newmoonhana.Runtime.Tools.PopupUI.Window;
using UnityEngine;

namespace GalaxyIO.Tools.PopupUI.Window
{
    public class WindowRespawn : WindowBase
    {
        /// <summary>
        /// 리스폰 버튼. 요청이 접수되면 태그가 지워져 창이 닫힘.
        /// </summary>
        public void OnPressRespawn()
        {
            ClientNetworkRequestManager.RequestRespawn();
        }
    }

}
