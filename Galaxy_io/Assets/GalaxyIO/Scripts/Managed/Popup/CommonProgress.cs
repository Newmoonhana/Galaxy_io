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

        // 씬 전환을 넘어가는 팝업. 떠 있는 동안만 존재함.
        private static CommonProgress acrossScenes;

        // 프리팹 로드 대기 중. 중복 호출 방지.
        private static bool opening;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStaticData()
        {
            acrossScenes = null;
            opening = false;
        }

        /// <summary>
        /// 작업이 끝날 때까지 진행 팝업 표시. 작업 결과를 그대로 반환.
        /// </summary>
        public static async Awaitable<T> Show<T>(Transform _parent, Func<Awaitable<T>> _task)
        {
            CommonProgress progress = await Create(_parent);

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

        /// <summary>
        /// 씬이 바뀌어도 유지되는 진행 팝업 표시. 세션 시작처럼 대기 구간이 씬 전환을
        /// 넘어갈 때 씀. 닫는 시점은 부르는 쪽이 <see cref="HideAcrossScenes"/>로 정함.
        /// </summary>
        public static async Awaitable ShowAcrossScenes()
        {
            if (acrossScenes != null || opening) return;

            opening = true;
            CommonProgress progress = await Create(null);

            // 씬이 바뀌면 UI 카메라가 사라짐. 카메라가 필요 없는 Overlay 로 고정함.
            if (progress.Cv != null) progress.Cv.renderMode = RenderMode.ScreenSpaceOverlay;
            DontDestroyOnLoad(progress.gameObject);

            // 로드를 기다리는 동안 닫혔으면 띄우지 않음.
            if (opening) acrossScenes = progress;
            else progress.Close();

            opening = false;
        }

        /// <summary>
        /// <see cref="ShowAcrossScenes"/>로 띄운 팝업을 치움. 여러 번 불러도 안전함.
        /// </summary>
        public static void HideAcrossScenes()
        {
            opening = false;

            if (acrossScenes == null) return;

            acrossScenes.Close();
            acrossScenes = null;
        }

        // 프리팹을 불러 인스턴스를 만듦.
        private static async Awaitable<CommonProgress> Create(Transform _parent)
        {
            await AddressableAssetsManager.gameObject_lst.LoadData(key);
            GameObject popupObj = Instantiate(AddressableAssetsManager.gameObject_lst.GetData(key).Data, _parent);
            AddressableAssetsManager.gameObject_lst.GetData(key).UnLoad();

            return popupObj.GetComponent<CommonProgress>();
        }
    }
}
