using System;
using Newmoonhana.Runtime.Tools.PopupUI.Popup;
using Newmoonhana.Runtime.Utils;
using UnityEngine;

namespace GalaxyIO.Tools.PopupUI.Popup
{
    /// <summary>
    /// 서버 비동기 작업 대기용 진행 팝업.
    /// </summary>
    public class CommonProgress : PopupBase
    {
        private const string key = "Common_Progress";

        /// <summary>
        /// 작업이 끝날 때까지 진행 팝업 표시. 작업 결과를 그대로 반환.
        /// </summary>
        public static async Awaitable<T> Show<T>(Transform _parent, Func<Awaitable<T>> _task)
        {
            await AddressableAssetsManager.gameObject_lst.LoadData(key);
            GameObject popupObj = Instantiate(AddressableAssetsManager.gameObject_lst.GetData(key).Data, _parent);
            AddressableAssetsManager.gameObject_lst.GetData(key).UnLoad();
            CommonProgress progress = popupObj.GetComponent<CommonProgress>();

            try
            {
                return await _task();
            }
            finally
            {
                // 대기 중 부모가 삭제됐으면 같이 삭제된 상태.
                if (progress != null)
                    progress.Close();
            }
        }

        public void Close()
        {
            Destroy(gameObject);
        }
    }
}
