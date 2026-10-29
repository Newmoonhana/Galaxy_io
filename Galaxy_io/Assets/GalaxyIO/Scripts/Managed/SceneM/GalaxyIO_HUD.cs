using Newmoonhana.Runtime.Utils.Pattern;
using UnityEngine;

namespace GalaxyIO.Main
{
    /// <summary>
    /// 인게임 HUD 씬의 루트. 메인 씬과의 연결 지점.
    /// </summary>
    public sealed class GalaxyIO_HUD : Singleton<GalaxyIO_HUD>
    {
        public const string SceneName = "GalaxyIO_HUD";

        [SerializeField]
        [Tooltip("인게임에서만 보이는 HUD 캔버스")]
        private Canvas hud;

        public Canvas Hud => hud;

        /// <summary>
        /// 메인 씬의 UI 카메라를 HUD 캔버스에 연결.
        /// </summary>
        public void Bind(UnityEngine.Camera uiCamera)
        {
            hud.worldCamera = uiCamera;
        }
    }
}
