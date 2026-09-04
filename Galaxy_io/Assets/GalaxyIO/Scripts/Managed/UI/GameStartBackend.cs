using GalaxyIO.DOTS.Client.Networking;
using UnityEngine;

namespace GalaxyIO.Scripts.Managed.UI
{
    /// <summary>
    /// 게임 시작 버튼의 동작을 처리하는 UI 백엔드.
    /// </summary>
    public sealed class GameStartBackend : MonoBehaviour
    {
        // 게임 시작 후 숨길 시작 화면
        [SerializeField]
        public GameObject Screen;

        /// <summary>
        /// 스폰 요청이 접수되면 시작 화면을 닫음.
        /// </summary>
        public void OnPressGameStart()
        {
            if (ClientNetworkRequestManager.RequestPlayerSpawn())
            {
                Screen.SetActive(false);
            }
        }
    }
}