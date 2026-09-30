using System;
using GalaxyIO.DOTS.Client.Networking;
using GalaxyIO.DOTS.Client.Components.Message;
using GalaxyIO.DOTS.Networking;
using Unity.Collections;
using GalaxyIO.Tools;
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
        // 끊김 사유. 안내창 본문에 표기함.
        private EntityQuery disconnectQuery;
        // 접속 실패(핸드셰이크 전 끊김) 태그.
        private EntityQuery connectFailedQuery;
        private bool hasQueries;
        // 캐시한 쿼리가 속한 클라이언트 월드. 세션 종료 시 Dispose 되므로 매 프레임 확인함.
        private World clientWorld;
        private RespawnUI respawnUI;
        private GameMenuUI gameMenuUI;
        private CommonMessageBox disconnectedMsgBox;
        // 옵션 팝업 로드 중 중복 입력 방지.
        private bool isOptionLoading;
        // 안내창 로드 중 중복 생성 방지.
        private bool isMsgBoxLoading;
        // 직전 프레임의 인게임 여부.
        private bool wasInGame;
        // 나가기로 끊는 중. 이때는 안내창을 띄우지 않음.
        private bool isQuitting;
        // 게임 오버로 리스폰 대기 중. 인게임 재시작까지 게임 메뉴를 막음.
        private bool isGameOver;

        protected void Start()
        {
            _ = AddressableAssetsManager.gameObject_lst.LoadData(respawnUIKey);
            _ = AddressableAssetsManager.gameObject_lst.LoadData(gameMenuUIKey);

            clientWorld = ClientServerBootstrap.ClientWorld;
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
                disconnectQuery = clientWorld.EntityManager.CreateEntityQuery(
                    ComponentType.ReadOnly<ClientDisconnectedMessage>()
                );
                connectFailedQuery = clientWorld.EntityManager.CreateEntityQuery(
                    ComponentType.ReadOnly<PlayerConnectionFailedTag>()
                );
                hasQueries = true;
            }
            else
            {
                // 월드가 없으면 더 기다릴 것이 없음. 로딩 오버레이를 치움.
                CommonProgress.HideAcrossScenes();
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
            // 세션을 나가면 클라이언트 월드가 Dispose 됨. 캐시한 쿼리를 더 쓰면 안 됨.
            if (hasQueries && !ClientWorldScope.IsAlive(clientWorld)) hasQueries = false;

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

            // 접속까지 끝났으면 로딩 오버레이를 치움.
            if (visible) CommonProgress.HideAcrossScenes();

            if (hud == null || hud.enabled == visible) return;

            hud.enabled = visible;
        }

        // 게임 오버 / 인게임 재시작 전환 감지. PlayerDeathTag 생성·삭제가 기준.
        private void UpdateRespawnUI()
        {
            bool dead = !deathQuery.IsEmpty;
            if (dead == (respawnUI != null)) return;

            if (dead) OnGameOver();
            else OnGameRestart();
        }

        // 게임 오버. 리스폰 창을 띄우고 게임 메뉴를 막음. 열려 있던 메뉴는 닫음.
        private void OnGameOver()
        {
            GameObject prefab = AddressableAssetsManager.gameObject_lst.GetData(respawnUIKey).Data;
            respawnUI = Instantiate(prefab).GetComponent<RespawnUI>();
            respawnUI.AddListener(QuitGame);

            isGameOver = true;
            CloseGameMenu();
        }

        // 인게임 재시작. 리스폰 창을 치우고 게임 메뉴를 다시 허용.
        private void OnGameRestart()
        {
            Destroy(respawnUI.gameObject);
            respawnUI = null;

            isGameOver = false;
        }

        // 나가기 없이 인게임이 끝나면 서버와 끊긴 것. 안내창 생성, 버튼을 누르면 로비로 나감.
        private void UpdateDisconnectedUI()
        {
            bool inGame = !inGameQuery.IsEmpty;
            bool lost = wasInGame && !inGame && !isQuitting;

            wasInGame = inGame;
            if (!inGame) isQuitting = false;

            // 접속 자체가 실패한 경우. 인게임에 들어가 본 적이 없으므로 따로 알림.
            if (!connectFailedQuery.IsEmpty)
            {
                ClientServerBootstrap.ClientWorld.EntityManager.DestroyEntity(connectFailedQuery);

                if (disconnectedMsgBox == null && !isMsgBoxLoading)
                {
                    _ = OpenConnectFailedMsgBox();
                }

                return;
            }

            if (!lost || disconnectedMsgBox != null || isMsgBoxLoading) return;

            // 리스폰 창과 대기 중인 요청 정리.
            ClientNetworkRequestManager.RequestQuit();
            _ = OpenDisconnectedMsgBox();
        }

        private async Awaitable OpenDisconnectedMsgBox()
        {
            isMsgBoxLoading = true;

            // 안내창이 오버레이에 가리지 않게 먼저 치움.
            CommonProgress.HideAcrossScenes();

            await AddressableAssetsManager.gameObject_lst.LoadData(msgBoxKey);
            var data = AddressableAssetsManager.gameObject_lst.GetData(msgBoxKey);
            disconnectedMsgBox = Instantiate(data.Data).GetComponent<CommonMessageBox>();
            data.UnLoad();

            disconnectedMsgBox.SetWinText("Disconnected", $"You've disconnected from the server. Please rejoin.\n({TakeDisconnectReason()})");
            disconnectedMsgBox.Refresh();
            disconnectedMsgBox.AddListener(QuitGame);

            isMsgBoxLoading = false;
        }

        // 끊김 사유를 하나 꺼내고 메시지를 정리함. 없으면 UNKNOWN.
        private string TakeDisconnectReason()
        {
            if (!hasQueries || disconnectQuery.IsEmpty) return NetworkMsg.UNKNOWN;

            EntityManager entityManager = ClientServerBootstrap.ClientWorld.EntityManager;

            using NativeArray<Entity> entities = disconnectQuery.ToEntityArray(Allocator.Temp);
            string reason = entityManager.GetComponentData<ClientDisconnectedMessage>(entities[0]).Reason.ToString();

            entityManager.DestroyEntity(disconnectQuery);
            return reason;
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

            // 게임 오버 중엔 ESC 무시. 리스폰 창의 나가기만 남김.
            if (isGameOver) return;

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

            // 월드를 버리고 로비 씬으로 돌아감. 호스트였다면 서버도 함께 닫힘.
            _ = GameSession.Leave();
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

        // 접속 실패 안내. 확인을 누르면 로비로 돌아감.
        private async Awaitable OpenConnectFailedMsgBox()
        {
            isMsgBoxLoading = true;

            // 안내창이 오버레이에 가리지 않게 먼저 치움.
            CommonProgress.HideAcrossScenes();

            await AddressableAssetsManager.gameObject_lst.LoadData(msgBoxKey);
            var data = AddressableAssetsManager.gameObject_lst.GetData(msgBoxKey);
            disconnectedMsgBox = Instantiate(data.Data).GetComponent<CommonMessageBox>();
            data.UnLoad();

            disconnectedMsgBox.SetWinText(L10n.Get("GameStart/FailedTitle"), L10n.Get("GameStart/Unreachable"));
            disconnectedMsgBox.Refresh();
            disconnectedMsgBox.AddListener(QuitGame);

            isMsgBoxLoading = false;
        }
    }

}
