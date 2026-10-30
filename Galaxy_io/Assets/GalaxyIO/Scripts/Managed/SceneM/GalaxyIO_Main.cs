using System;
using GalaxyIO.DOTS.Client.Networking;
using GalaxyIO.DOTS.Client.Components.Message;
using GalaxyIO.DOTS.Client.Components.Singleton;
using GalaxyIO.DOTS.Networking;
using GalaxyIO.Scripts.Managed.Background;
using Unity.Collections;
using GalaxyIO.Tools;
using GalaxyIO.Tools.PopupUI.Popup;
using Newmoonhana.Runtime.Tools.PopupUI;
using Newmoonhana.Runtime.Tools.PopupUI.Popup;
using Newmoonhana.Runtime.Utils;
using Newmoonhana.Runtime.Utils.DEBUG;
using Newmoonhana.Runtime.Utils.Pattern;
using Unity.Entities;
using Unity.NetCode;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

namespace GalaxyIO.Main
{
    public class GalaxyIO_Main : Singleton<GalaxyIO_Main>
    {
        private const string respawnUIKey = "Respawn_UI";
        private const string gameMenuUIKey = "GameMenu_UI";
        private const string inventoryUIKey = "Inventory_UI";

        [SerializeField]
        [Tooltip("게임 메뉴 열기/닫기 (ESC)")]
        private InputActionReference menuAction;

        [SerializeField]
        [Tooltip("인벤토리 열기/닫기 (E)")]
        private InputActionReference inventoryAction;

        [SerializeField]
        [Tooltip("HUD 캔버스를 그리는 UI 카메라")]
        private UnityEngine.Camera uiCamera;

        // 이 컴포넌트가 로드한 HUD 씬인지. 로드 중에 메인 씬이 내려가면 false 로 바뀜.
        private bool ownsHudScene;

        // 로컬 플레이어 상태. 메뉴/인벤토리 열림, 파괴, 접속 실패.
        private EntityQuery stateQuery;
        // 서버 접속 중(=인게임)인지 판별하는 쿼리.
        private EntityQuery inGameQuery;
        // 끊김 사유. 안내창 본문에 표기함.
        private EntityQuery disconnectQuery;
        private bool hasQueries;
        // 캐시한 쿼리가 속한 클라이언트 월드. 세션 종료 시 Dispose 되므로 매 프레임 확인함.
        private World clientWorld;
        private RespawnUI respawnUI;
        private GameMenuUI gameMenuUI;
        private InventoryUI inventoryUI;
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
            // 조작법 팝업. 로딩 중에는 none 상태로 떠 있음
            ControlUI.Open().Forget();

            clientWorld = ClientServerBootstrap.ClientWorld;
            if (clientWorld != null)
            {
                stateQuery = clientWorld.EntityManager.CreateEntityQuery(
                    ComponentType.ReadWrite<PlayerStateComponent>()
                );
                inGameQuery = clientWorld.EntityManager.CreateEntityQuery(
                    ComponentType.ReadOnly<NetworkStreamInGame>()
                );
                disconnectQuery = clientWorld.EntityManager.CreateEntityQuery(
                    ComponentType.ReadOnly<ClientDisconnectedMessage>()
                );
                hasQueries = true;

                // 월드가 없으면 HUD 는 어차피 안 보임. 디버그 진입은 월드를 만든 뒤 메인 씬을 다시 올리므로
                // 그쪽 인스턴스가 로드함.
                LoadHUD().Forget();
            }
            else
            {
                // 월드가 없으면 더 기다릴 것이 없음. 로딩 오버레이를 치우고 배경도 바로 보여 줌
                // (UpdateHUD 의 visible 이 영원히 false 라 거기서는 배경이 안 나타남).
                CommonProgress.HideAcrossScenes();
                BackgroundView.RevealAcrossScenes();
            }
            
            // 오브젝트가 파괴될 때 자동으로 멈추도록 destroyCancellationToken 전달
            RunUpdateLoop(this.destroyCancellationToken).Forget();
        }

        private void OnEnable()
        {
            menuAction?.action.Enable();
            inventoryAction?.action.Enable();
        }

        private void OnDisable()
        {
            menuAction?.action.Disable();
            inventoryAction?.action.Disable();
        }

        // 종료 버튼 없이 앱을 꺼도 서버에 접속 종료를 알림.
        private void OnApplicationQuit()
        {
            ClientNetworkRequestManager.DisconnectImmediately();
        }

        private async Awaitable RunUpdateLoop(System.Threading.CancellationToken cancellationToken)
        {
            // 버려진 Awaitable 은 예외를 삼키고 루프만 조용히 멈춤. 직접 잡아 남김.
            try
            {
                // 리스폰 / 게임 메뉴 / 인벤토리 창은 GetData 로 바로 띄움. 웹은 동기 로드가 안 되므로
                // 로드가 끝난 뒤에 루프를 돌림.
                await AddressableAssetsManager.gameObject_lst.LoadData(respawnUIKey);
                await AddressableAssetsManager.gameObject_lst.LoadData(gameMenuUIKey);
                await AddressableAssetsManager.gameObject_lst.LoadData(inventoryUIKey);

                while (!cancellationToken.IsCancellationRequested)
                {
                    // 세션을 나가면 클라이언트 월드가 Dispose 됨. 캐시한 쿼리를 더 쓰면 안 됨.
                    if (hasQueries && !ClientWorldScope.IsAlive(clientWorld)) hasQueries = false;

                    UpdateHUD();

                    if (hasQueries)
                    {
                        UpdateDisconnectedUI();

                        // `UpdateDisconnectedUI`가 세션을 끝냈으면 쿼리가 이미 죽어 있음.
                        if (hasQueries)
                        {
                            UpdateRespawnUI();
                            UpdateGameMenuUI();
                            UpdateInventoryUI();
                        }
                    }

                    await Awaitable.NextFrameAsync(cancellationToken);
                }
            }
            catch (OperationCanceledException)
            {
                // 오브젝트가 파괴되어 멈춤. 정상 종료.
            }
            catch (Exception e)
            {
                DebugLog.LogError(e.ToString(), this);
            }
        }

        // 인게임에서만 HUD 표시. 뷰 스크립트는 계속 돌도록 캔버스만 끔.
        private void UpdateHUD()
        {
            bool visible = hasQueries && !inGameQuery.IsEmpty;

            // 접속까지 끝났으면 로딩 오버레이를 치움.
            // 배경은 여기서 켜지 않음 — 접속 완료가 카메라 순간이동보다 몇 프레임 빨라서
            // 아직 원점에 있는 배경이 잠깐 보임. FollowPlayerEntity 가 카메라를 옮긴 뒤 켬.
            if (visible)
            {
                CommonProgress.HideAcrossScenes();
                // 로딩이 끝났으므로 조작법 창을 펼침. 두 번째 호출부터는 무시됨
                ControlUI.Opened?.OnLoadComplete();
            }

            // 디버그 진입에서 먼저 뜬 메인 씬도 루프가 계속 돎. 자기가 올린 HUD 만 건드림.
            if (!ownsHudScene || !GalaxyIO_HUD.HasInstance) return;

            Canvas hud = GalaxyIO_HUD.Instance.Hud;
            if (hud.enabled == visible) return;

            hud.enabled = visible;
        }

        // HUD 씬을 Additive 로 로드하고 UI 카메라 연결.
        private async Awaitable LoadHUD()
        {
            // SceneStack.Push 는 로드 직후 마지막 씬을 자기 프레임으로 집음. 그 전에 HUD 로드를 시작하면
            // HUD 씬이 집힐 수 있어, 이 씬이 활성 씬으로 잡힐 때까지 기다림.
            while (SceneManager.GetActiveScene() != gameObject.scene)
            {
                await Awaitable.NextFrameAsync();
                if (this == null) return;
            }

            // 에디터에서 미리 열어 둔 HUD 씬은 월드가 생기기 전에 뷰의 Start 가 돌아 꺼져 있음. 새로 로드함.
            Scene opened = SceneManager.GetSceneByName(GalaxyIO_HUD.SceneName);
            if (opened.isLoaded)
            {
                // 이미 언로드 중이면 null 이 옴 (나가기 직후 재입장).
                AsyncOperation unload = SceneManager.UnloadSceneAsync(opened);
                if (unload != null) await unload;
            }

            // 기다리는 동안 메인 씬이 내려감.
            if (this == null) return;

            ownsHudScene = true;
            await SceneManager.LoadSceneAsync(GalaxyIO_HUD.SceneName, LoadSceneMode.Additive);

            // 로드 중 메인 씬이 내려갔으면 `UnloadHUD`가 아직 로드 안 된 씬을 못 내렸음. 여기서 내림.
            if (!ownsHudScene)
            {
                SceneManager.UnloadSceneAsync(GalaxyIO_HUD.SceneName);
                return;
            }

            if (GalaxyIO_HUD.HasInstance) GalaxyIO_HUD.Instance.Bind(uiCamera);
        }

        // 게임 오버 / 인게임 재시작 전환 감지. PlayerStateComponent.Dead 가 기준.
        private void UpdateRespawnUI()
        {
            bool dead = stateQuery.GetSingleton<PlayerStateComponent>().Dead;
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
            CloseInventory();
        }

        // 인게임 재시작. 리스폰 창을 치우고 게임 메뉴를 다시 허용.
        private void OnGameRestart()
        {
            respawnUI.Close();
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
            if (stateQuery.GetSingleton<PlayerStateComponent>().ConnectionFailed)
            {
                stateQuery.GetSingletonRW<PlayerStateComponent>().ValueRW.ConnectionFailed = false;

                // 안내는 로비로 돌아간 뒤 로비 화면 위에 띄움. 여기서는 바로 내려감.
                LobbyReturn.SetNotice(L10n.Get("GameStart/FailedTitle"), L10n.Get("GameStart/Unreachable"));
                QuitGame();
                return;
            }

            if (!lost || disconnectedMsgBox != null || isMsgBoxLoading) return;

            // 리스폰 창과 대기 중인 요청 정리.
            ClientNetworkRequestManager.RequestQuit();
            OpenDisconnectedMsgBox().Forget();
        }

        private async Awaitable OpenDisconnectedMsgBox()
        {
            isMsgBoxLoading = true;

            // 안내창이 오버레이에 가리지 않게 먼저 치움.
            CommonProgress.HideAcrossScenes();

            disconnectedMsgBox = await CommonMessageBox.Open();
            if (disconnectedMsgBox == null) return;

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

        // ESC로 게임 메뉴 열기. 닫기는 떠 있는 팝업이 직접 처리함.
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

            // 채팅 입력 중의 ESC 는 입력창만 닫음.
            if (stateQuery.GetSingleton<PlayerStateComponent>().ChatFocused) return;

            if (menuAction == null || !menuAction.action.WasPressedThisFrame()) return;

            if (gameMenuUI != null) return;

            // 떠 있는 팝업이 ESC 를 먹음. 여기서는 여는 것만 담당함
            if (PopupOrderStack.IsTopCancelable) return;

            OpenGameMenu();
        }

        private void OpenGameMenu()
        {
            GameObject prefab = AddressableAssetsManager.gameObject_lst.GetData(gameMenuUIKey).Data;
            gameMenuUI = Instantiate(prefab).GetComponent<GameMenuUI>();
            gameMenuUI.AddListener(CloseGameMenu, Instance.OnPressOption, QuitGame);

            stateQuery.GetSingletonRW<PlayerStateComponent>().ValueRW.GameMenuOpened = true;
        }

        private void CloseGameMenu()
        {
            if (gameMenuUI == null) return;

            gameMenuUI.Close();
            gameMenuUI = null;

            stateQuery.GetSingletonRW<PlayerStateComponent>().ValueRW.GameMenuOpened = false;
        }

        // E로 인벤토리 토글. 인게임에서 게임 메뉴가 닫혀 있을 때만 열림.
        private void UpdateInventoryUI()
        {
            // 접속이 끊기면 인벤토리도 닫음.
            if (inGameQuery.IsEmpty)
            {
                CloseInventory();
                return;
            }

            if (isGameOver || gameMenuUI != null) return;

            // 채팅 입력 중의 E 는 글자로만 씀.
            if (stateQuery.GetSingleton<PlayerStateComponent>().ChatFocused) return;

            if (inventoryAction == null || !inventoryAction.action.WasPressedThisFrame()) return;

            if (inventoryUI == null)
            {
                OpenInventory();
                return;
            }

            // 다른 팝업이 위에 떠 있으면 그 팝업이 입력을 가짐
            if (!PopupOrderStack.IsTop(inventoryUI)) return;

            // 인벤토리 안에서는 E 를 ESC 와 같게 씀. 사양 창이 열려 있으면 그것만 닫음
            inventoryUI.TryCancel();
        }

        private void OpenInventory()
        {
            GameObject prefab = AddressableAssetsManager.gameObject_lst.GetData(inventoryUIKey).Data;
            inventoryUI = Instantiate(prefab).GetComponent<InventoryUI>();
            inventoryUI.AddListener(CloseInventory);

            stateQuery.GetSingletonRW<PlayerStateComponent>().ValueRW.InventoryOpened = true;
        }

        private void CloseInventory()
        {
            if (inventoryUI == null) return;

            inventoryUI.Close();
            inventoryUI = null;

            stateQuery.GetSingletonRW<PlayerStateComponent>().ValueRW.InventoryOpened = false;
        }

        /// <summary>
        /// 서버 접속을 끊고 로비 메인으로 복귀.
        /// </summary>
        private void QuitGame()
        {
            isQuitting = true;
            ClientNetworkRequestManager.RequestQuit();
            CloseGameMenu();
            CloseInventory();

            // `Leave`는 첫 await 전에 월드를 버림. 이 줄 뒤로 캐시한 쿼리는 전부 무효.
            hasQueries = false;

            // 월드를 버리고 로비 씬으로 돌아감. 호스트였다면 서버도 함께 닫힘.
            GameSession.Leave().Forget();
        }

        public override void Release()
        {
            base.Release();
            AddressableAssetsManager.gameObject_lst.RemoveData(respawnUIKey);
            AddressableAssetsManager.gameObject_lst.RemoveData(gameMenuUIKey);
            AddressableAssetsManager.gameObject_lst.RemoveData(inventoryUIKey);
            // 조작법 팝업 키는 ControlUI 가 들고 있음
            ControlUI.Unload();
            UnloadHUD();
        }

        // SceneStack.Pop 은 최상단 프레임 씬만 내림. HUD 씬은 메인 씬과 함께 직접 내림.
        private void UnloadHUD()
        {
            if (!ownsHudScene) return;
            ownsHudScene = false;

            Scene hudScene = SceneManager.GetSceneByName(GalaxyIO_HUD.SceneName);
            if (hudScene.isLoaded) SceneManager.UnloadSceneAsync(hudScene);
        }

        /// <summary>
        /// 인게임 옵션 버튼 AddListener 연결용.
        /// </summary>
        public void OnPressOption()
        {
            if (isOptionLoading) return;
            OpenOption().Forget();
        }
        private async Awaitable OpenOption()
        {
            isOptionLoading = true;
            await CommonOption.Open();
            isOptionLoading = false;
        }

    }

}
