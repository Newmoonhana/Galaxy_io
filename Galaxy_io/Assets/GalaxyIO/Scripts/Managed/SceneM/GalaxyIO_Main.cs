using System;
using Newmoonhana.Runtime.Tools.PopupUI;
using Newmoonhana.Runtime.Utils;
using Newmoonhana.Runtime.Utils.Pattern;
using UnityEngine;

namespace GalaxyIO.Main
{
    public class GalaxyIO_Main : Singleton<GalaxyIO_Main>
    {
        protected void Start()
        {
            _ = LoadPopups();
        }

        private async Awaitable LoadPopups()
        {
            AddressableAssetsData<GameObject> popupLoginData = new AddressableAssetsData<GameObject>();
            await popupLoginData.Load("Popup_Login", true);
            Instantiate(popupLoginData.Data);
            popupLoginData.UnLoad();
        }
    }

}
