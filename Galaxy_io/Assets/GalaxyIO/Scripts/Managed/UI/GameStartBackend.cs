using GalaxyIO.DOTS.Bootstrap;
using GalaxyIO.DOTS.Client.Components.Message;
using GalaxyIO.DOTS.Client.Networking;
using GalaxyIO.DOTS.Components.Actor;
using TMPro;
using Unity.Entities;
using Unity.NetCode;
using Unity.Networking.Transport;
using UnityEngine;
using UnityEngine.UI;

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
        [Tooltip("플레이어 이름 입력창")]
        private TMP_InputField nameField;

        [SerializeField]
        [Tooltip("이름이 비었을 때 띄울 안내문")]
        private TMP_Text nameWarning;

        [SerializeField]
        [Tooltip("주소를 읽지 못했을 때 띄울 안내문")]
        private TMP_Text warning;

        [SerializeField]
        [Tooltip("서버 접속에 실패했을 때 띄울 안내문")]
        private TMP_Text unreachable;

        [SerializeField]
        [Tooltip("게임 시작 버튼")]
        private Button startButton;

        [SerializeField]
        [Tooltip("루프백 버튼")]
        private Button loopbackButton;

        private EntityQuery requestQuery;
        private EntityQuery failedQuery;

        // 접속 결과를 기다리는 중.
        private bool connecting;

        private void Awake()
        {
            warning.gameObject.SetActive(false);
            unreachable.gameObject.SetActive(false);
            nameWarning.gameObject.SetActive(false);
            nameField.characterLimit = PlayerNameComponent.MaxLength;
        }

        private void Start()
        {
            World clientWorld = ClientServerBootstrap.ClientWorld;

            if (clientWorld == null)
            {
                enabled = false;
                return;
            }

            requestQuery = clientWorld.EntityManager.CreateEntityQuery(
                ComponentType.ReadOnly<PlayerSpawnRequest>()
            );

            failedQuery = clientWorld.EntityManager.CreateEntityQuery(
                ComponentType.ReadOnly<PlayerConnectionFailedTag>()
            );
        }

        private void Update()
        {
            if (!connecting)
            {
                return;
            }

            if (!failedQuery.IsEmpty)
            {
                ClientServerBootstrap.ClientWorld.EntityManager.DestroyEntity(failedQuery);
                unreachable.gameObject.SetActive(true);
                SetConnecting(false);
                return;
            }

            // 요청이 처리됐고 실패 태그가 없으면 접속 성공.
            if (requestQuery.IsEmpty)
            {
                Screen.SetActive(false);
                SetConnecting(false);
            }
        }

        /// <summary>
        /// 루프백 주소를 입력창에 채움. 접속은 하지 않음.
        /// </summary>
        public void OnPressLoopback()
        {
            addressField.Fill(NetcodeBootstrap.LoopbackAddress);
            warning.gameObject.SetActive(false);
            unreachable.gameObject.SetActive(false);
            nameWarning.gameObject.SetActive(false);
        }

        /// <summary>
        /// 스폰을 요청함. 시작 화면은 접속에 성공하면 닫힘.
        /// </summary>
        public void OnPressGameStart()
        {
            unreachable.gameObject.SetActive(false);

            // 공백만 입력한 경우도 빈 이름으로 취급함.
            string playerName = nameField.text.Trim();

            if (playerName.Length == 0)
            {
                warning.gameObject.SetActive(false);
                nameWarning.gameObject.SetActive(true);
                return;
            }

            nameWarning.gameObject.SetActive(false);

            // 모양이 맞아도 값이 범위를 넘으면(e.g. 999.0.0.1) 여기서 걸림.
            if (!NetworkEndpoint.TryParse(
                    addressField.Address,
                    NetcodeBootstrap.Port,
                    out NetworkEndpoint endpoint,
                    NetworkFamily.Ipv4
                ))
            {
                warning.gameObject.SetActive(true);
                return;
            }

            if (ClientNetworkRequestManager.RequestPlayerSpawn(endpoint, playerName))
            {
                warning.gameObject.SetActive(false);
                SetConnecting(true);
            }
        }

        /// <summary>
        /// 접속 대기 상태 전환. 대기 중에는 버튼을 잠금.
        /// </summary>
        private void SetConnecting(bool value)
        {
            connecting = value;
            startButton.interactable = !value;
            loopbackButton.interactable = !value;
            nameField.interactable = !value;
        }
    }
}