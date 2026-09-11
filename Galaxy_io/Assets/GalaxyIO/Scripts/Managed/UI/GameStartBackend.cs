using GalaxyIO.DOTS.Bootstrap;
using GalaxyIO.DOTS.Client.Networking;
using TMPro;
using Unity.Networking.Transport;
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

        [SerializeField]
        [Tooltip("서버 주소 입력창")]
        private IpAddressField addressField;

        [SerializeField]
        [Tooltip("주소를 읽지 못했을 때 띄울 안내문")]
        private TMP_Text warning;

        private void Awake()
        {
            warning.gameObject.SetActive(false);
        }

        /// <summary>
        /// 루프백 주소를 입력창에 채움. 접속은 하지 않음.
        /// </summary>
        public void OnPressLoopback()
        {
            addressField.Fill(NetcodeBootstrap.LoopbackAddress);
            warning.gameObject.SetActive(false);
        }

        /// <summary>
        /// 스폰 요청이 접수되면 시작 화면을 닫음.
        /// </summary>
        public void OnPressGameStart()
        {
            // 모양이 맞아도 값이 범위를 넘으면(e.g. 999.0.0.1) 여기서 걸림.
            if (!NetworkEndpoint.TryParse(
                    addressField.Address,
                    NetcodeBootstrap.Port,
                    out NetworkEndpoint endpoint,
                    NetworkFamily.Ipv4))
            {
                warning.gameObject.SetActive(true);
                return;
            }

            if (ClientNetworkRequestManager.RequestPlayerSpawn(endpoint))
            {
                Screen.SetActive(false);
            }
        }
    }
}