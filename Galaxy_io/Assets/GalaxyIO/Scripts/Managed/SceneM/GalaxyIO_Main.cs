using System;
using GalaxyIO.DOTS.Client.Networking;
using GalaxyIO.DOTS.Client.Components.Message;
using GalaxyIO.Tools.PopupUI.Popup;
using Newmoonhana.Runtime.Tools.PopupUI;
using Newmoonhana.Runtime.Utils;
using Newmoonhana.Runtime.Utils.Pattern;
using Unity.Entities;
using Unity.NetCode;
using UnityEngine;
using UnityEngine.InputSystem;

namespace GalaxyIO.Main
{
    public class GalaxyIO_Main : Singleton<GalaxyIO_Main>
    {
        private const string respawnUIKey = "Respawn_UI";
        private const string gameMenuUIKey = "GameMenu_UI";
        private const string msgBoxKey = "Common_MsgBox";

        [SerializeField]
        [Tooltip("게임 메뉴 열기/닫기 (ESC)")]
        private InputActionReference menuAction;

        [SerializeField]
        [Tooltip("인게임에서만 보이는 HUD 캔버스")]
        private Canvas hud;

        // 파괴된 동안(=리스폰 대기)인지 판별하는 쿼리.
        private EntityQuery deathQuery;
        // 서버 접속 중(=인게임)인지 판별하는 쿼리.
        private EntityQuery inGameQuery;
        // 게임 메뉴 태그. 열린 동안 우주선 조작을 막음.
        private EntityQuery menuOpenQuery;
        private bool hasQueries;
        private RespawnUI respawnUI;
        private GameMenuUI gameMenuUI;
        private CommonMessageBox disconnectedMsgBox;
        private LobbyUI lobbyUI;
        // 옵션 팝업 로드 중 중복 입력 방지.
        private bool isOptionLoading;
        // 안내창 로드 중 중복 생성 방지.
        private bool isMsgBoxLoading;
        // 직전 프레임의 인게임 여부.
        private bool wasInGame;
        // 나가기로 끊는 중. 이때는 안내창을 띄우지 않음.
        private bool isQuitting;

        protected void Start()
        {
            _ = LoadLobbyUI();
            _ = AddressableAssetsManager.gameObject_lst.LoadData(respawnUIKey);
            _ = AddressableAssetsManager.gameObject_lst.LoadData(gameMenuUIKey);

            World clientWorld = ClientServerBootstrap.ClientWorld;
            if (clientWorld != null)
            {
                deathQuery = clientWorld.EntityManager.CreateEntityQuery(
                    ComponentType.ReadOnly<PlayerDeathTag>()
                );
                inGameQuery = clientWorld.EntityManager.CreateEntityQuery(
                    ComponentType.ReadOnly<NetworkStreamInGame>()
                );
                menuOpenQuery = clientWorld.EntityManager.CreateEntityQuery(
                    ComponentType.ReadOnly<GameMenuOpenTag>()
                );
                hasQueries = true;
            }
        }

        private void OnEnable()
        {
            menuAction?.action.Enable();
        }

        private void OnDisable()
        {
            menuAction?.action.Disable();
        }

        // 종료 버튼 없이 앱을 꺼도 서버에 접속 종료를 알림.
        private void OnApplicationQuit()
        {
            ClientNetworkRequestManager.DisconnectImmediately();
        }

        private void Update()
        {
            UpdateHUD();

            if (!hasQueries) return;

            UpdateDisconnectedUI();
            UpdateRespawnUI();
            UpdateGameMenuUI();
        }

        // 인게임에서만 HUD 표시. 뷰 스크립트는 계속 돌도록 캔버스만 끔.
        private void UpdateHUD()
        {
            bool visible = hasQueries && !inGameQuery.IsEmpty;
            if (hud == null || hud.enabled == visible) return;

            hud.enabled = visible;
        }

        // 파괴 시 리스폰 UI 생성, 리스폰 시 삭제.
        private void UpdateRespawnUI()
        {
            bool dead = !deathQuery.IsEmpty;
            if (dead == (respawnUI != null)) return;

            if (dead)
            {
                GameObject prefab = AddressableAssetsManager.gameObject_lst.GetData(respawnUIKey).Data;
                respawnUI = Instantiate(prefab).GetComponent<RespawnUI>();
                respawnUI.AddListener(QuitGame);
            }
            else
            {
                Destroy(respawnUI.gameObject);
                respawnUI = null;
            }
        }

        // 나가기 없이 인게임이 끝나면 서버와 끊긴 것. 안내창 생성, 버튼을 누르면 로비로 나감.
        private void UpdateDisconnectedUI()
        {
            bool inGame = !inGameQuery.IsEmpty;
            bool lost = wasInGame && !inGame && !isQuitting;

            wasInGame = inGame;
            if (!inGame) isQuitting = false;

            if (!lost || disconnectedMsgBox != null || isMsgBoxLoading) return;

            // 리스폰 창과 대기 중인 요청 정리.
            ClientNetworkRequestManager.RequestQuit();
            _ = OpenDisconnectedMsgBox();
        }

        private async Awaitable OpenDisconnectedMsgBox()
        {
            isMsgBoxLoading = true;

            await AddressableAssetsManager.gameObject_lst.LoadData(msgBoxKey);
            var data = AddressableAssetsManager.gameObject_lst.GetData(msgBoxKey);
            disconnectedMsgBox = Instantiate(data.Data).GetComponent<CommonMessageBox>();
            data.UnLoad();

            disconnectedMsgBox.SetWinText("Disconnected", "You've disconnected from the server. Please rejoin.");
            disconnectedMsgBox.Refresh();
            disconnectedMsgBox.AddListener(QuitGame);

            isMsgBoxLoading = false;
        }

        // ESC로 게임 메뉴 토글. 인게임에서만 열림.
        private void UpdateGameMenuUI()
        {
            // 접속이 끊기면 메뉴도 닫음.
            if (inGameQuery.IsEmpty)
            {
                CloseGameMenu();
                return;
            }

            if (menuAction == null || !menuAction.action.WasPressedThisFrame()) return;

            if (gameMenuUI == null) OpenGameMenu();
            else CloseGameMenu();
        }

        private void OpenGameMenu()
        {
            GameObject prefab = AddressableAssetsManager.gameObject_lst.GetData(gameMenuUIKey).Data;
            gameMenuUI = Instantiate(prefab).GetComponent<GameMenuUI>();
            gameMenuUI.AddListener(CloseGameMenu, QuitGame);

            ClientServerBootstrap.ClientWorld.EntityManager.CreateEntity(typeof(GameMenuOpenTag));
        }

        private void CloseGameMenu()
        {
            if (gameMenuUI == null) return;

            Destroy(gameMenuUI.gameObject);
            gameMenuUI = null;

            ClientServerBootstrap.ClientWorld.EntityManager.DestroyEntity(menuOpenQuery);
        }

        /// <summary>
        /// 서버 접속을 끊고 로비 메인으로 복귀.
        /// </summary>
        private void QuitGame()
        {
            isQuitting = true;
            ClientNetworkRequestManager.RequestQuit();
            CloseGameMenu();

            if (lobbyUI != null) lobbyUI.ChangeState("main");
        }

        public override void Release()
        {
            base.Release();
            AddressableAssetsManager.gameObject_lst.RemoveData(respawnUIKey);
            AddressableAssetsManager.gameObject_lst.RemoveData(gameMenuUIKey);
        }

        /// <summary>
        /// 인게임 옵션 버튼 AddListener 연결용.
        /// </summary>
        public void OnPressOption()
        {
            if (isOptionLoading) return;
            _ = OpenOption();
        }
        private async Awaitable OpenOption()
        {
            isOptionLoading = true;
            await CommonOption.Open();
            isOptionLoading = false;
        }

        private async Awaitable LoadLobbyUI()
        {
            AddressableAssetsData<GameObject> lobbyUIData = new AddressableAssetsData<GameObject>();
            await lobbyUIData.Load("Lobby_UI", true);
            lobbyUI = Instantiate(lobbyUIData.Data).GetComponent<LobbyUI>();
            lobbyUIData.UnLoad();
        }
    }

}
