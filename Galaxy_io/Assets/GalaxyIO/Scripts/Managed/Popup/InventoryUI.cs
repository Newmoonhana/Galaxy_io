using GalaxyIO.DOTS.Authoring;
using GalaxyIO.DOTS.Authoring.Config;
using GalaxyIO.DOTS.Client.Components.Singleton;
using GalaxyIO.DOTS.Components.Actor;
using GalaxyIO.DOTS.Components.Message;
using GalaxyIO.DOTS.Components.Singleton;
using GalaxyIO.DOTS.Inventory;
using GalaxyIO.Tools.PopupUI.Window;
using Newmoonhana.Runtime.Tools.PopupUI.Popup;
using Newmoonhana.Runtime.Utils.Pattern;
using System.Collections.Generic;
using Unity.Collections;
using Unity.Entities;
using Unity.NetCode;
using UnityEngine;
using UnityEngine.Events;

namespace GalaxyIO.Tools.PopupUI.Popup
{
    /// <summary>
    /// 로컬 플레이어 우주선의 인벤토리 팝업. 메인 창, 강화 창, 우주선 / 포탑 사양 창을 관리함.
    /// 상점 상품을 누르면 사양 창을 열고, 사양 창의 장착 버튼이 서버에 구매 요청을 보냄.
    /// </summary>
    public class InventoryUI : PopupBase
    {
        private const string stateInventory = "inventory";
        private const string stateSpaceship = "spaceship";
        private const string stateTurret = "turret";

        #region IState
        private class InventoryState : IState
        {
            private WindowInventory win;
            public InventoryState(WindowInventory _win)
            {
                win = _win;
                OnExit();
            }

            public void UpdateState(float elapsedTime) {}
            public void Render() {}
            public void OnEnter(object p_data)
            {
                win.Open();
                win.Activate();
            }
            public void OnExit()
            {
                //메인 창은 사양 창 밑에 그대로 남음. 입력만 막음
                win.Deactivate();
            }
            public void Dispose()
            {
                win?.Dispose();
                win = null;
            }
        }

        /// <summary>
        /// 사양 창. 메인 창 위에 겹쳐 열리고 메인 창의 입력만 막음. 열 때 p_data 로 보여줄 상품을 받음.
        /// </summary>
        private class SpecState : IState
        {
            private WindowInventory inv_win;
            private WindowSpecBase win;
            private readonly System.Action<object> set;
            public SpecState(WindowInventory _invWin, WindowSpecBase _win, System.Action<object> _set)
            {
                inv_win = _invWin;
                win = _win;
                set = _set;
                OnExit();
            }

            public void UpdateState(float elapsedTime) {}
            public void Render() {}
            public void OnEnter(object p_data)
            {
                inv_win.Deactivate();

                win.Open();
                set(p_data);
                win.Activate();
            }
            public void OnExit()
            {
                win.Deactivate();
                win.Close();
            }
            public void Dispose()
            {
                win?.Dispose();
                win = null;
                inv_win = null;
            }
        }
        #endregion

        private WindowInventory inventory_win;
        private WindowUpgrade upgrade_win;
        private WindowSpaceshipSpec spaceship_win;
        private WindowTurretSpec turret_win;

        private EntityManager entityManager;
        private EntityQuery inventoryQuery;
        private EntityQuery registryQuery;
        private EntityQuery spaceshipRegistryQuery;

        // 강화, 장착 대상인 로컬 우주선
        private EntityQuery shipQuery;

        // 포탑 칸 번호별 장착 포탑 아이콘. 매 프레임 다시 채움.
        private readonly List<Sprite> turretIcons = new List<Sprite>();

        // 인벤토리 전체 닫기. 띄운 쪽이 상태 플래그까지 정리함.
        private UnityAction exit;

        // 캐시한 쿼리가 속한 클라이언트 월드. 세션 종료 시 Dispose 되므로 매 프레임 확인함.
        private World clientWorld;

        protected override void Awake()
        {
            base.Awake();

            inventory_win = transform.GetComponentInChildren<WindowInventory>(true);
            upgrade_win = transform.GetComponentInChildren<WindowUpgrade>(true);
            spaceship_win = transform.GetComponentInChildren<WindowSpaceshipSpec>(true);
            turret_win = transform.GetComponentInChildren<WindowTurretSpec>(true);
        }

        protected override void Start()
        {
            base.Start();

            inventory_win.OnSpaceshipPressed += spaceship => stateM.Change(stateSpaceship, spaceship);
            inventory_win.OnTurretPressed += recipe => stateM.Change(stateTurret, recipe);
            upgrade_win.OnUpgrade += OnUpgradePress;
            spaceship_win.OnEquip += OnSpaceshipEquip;
            spaceship_win.OnClosed += OnSpecClosed;
            turret_win.OnEquip += OnTurretEquip;
            turret_win.OnClosed += OnSpecClosed;

            stateM.Add(stateInventory, new InventoryState(inventory_win));
            stateM.Add(stateSpaceship, new SpecState(inventory_win, spaceship_win,
                data => spaceship_win.SetSpaceship((SpaceshipAsset)data)));
            stateM.Add(stateTurret, new SpecState(inventory_win, turret_win,
                data => turret_win.SetRecipe((RecipeAsset)data)));
            stateM.Change(stateInventory);

            clientWorld = ClientServerBootstrap.ClientWorld;

            if (clientWorld == null)
            {
                enabled = false;
                return;
            }

            entityManager = clientWorld.EntityManager;
            inventoryQuery = entityManager.CreateEntityQuery(ComponentType.ReadOnly<LocalInventoryComponent>());
            registryQuery = entityManager.CreateEntityQuery(ComponentType.ReadOnly<ItemRegistryAssetComponent>());
            spaceshipRegistryQuery = entityManager.CreateEntityQuery(ComponentType.ReadOnly<SpaceshipRegistryAssetComponent>());
            shipQuery = entityManager.CreateEntityQuery(
                ComponentType.ReadOnly<GhostOwnerIsLocal>(),
                ComponentType.ReadOnly<SpaceshipKindComponent>(),
                ComponentType.ReadOnly<EquippedTurretComponent>()
            );
        }

        protected override void OnDestroy()
        {
            base.OnDestroy();

            inventory_win = null;
            upgrade_win = null;
            spaceship_win = null;
            turret_win = null;
        }

        /// <summary>
        /// 생성 직후 호출. 닫기 처리는 생성한 쪽이 담당.
        /// </summary>
        public void AddListener(UnityAction _onExit)
        {
            inventory_win.AddListener(_onExit);
            exit = _onExit;
        }

        /// <summary>
        /// ESC / E. 사양 창이 열려 있으면 그것만 닫고, 아니면 인벤토리를 닫음.
        /// </summary>
        public override void TryCancel()
        {
            if (isClosing) return;

            if (stateM != null && stateM.CurrentToString != stateInventory)
            {
                OnSpecClosed();
                return;
            }

            exit?.Invoke();
        }

        private void LateUpdate()
        {
            // 세션을 나가면 클라이언트 월드가 Dispose 됨. 캐시한 쿼리를 더 쓰면 안 됨.
            if (!ClientWorldScope.IsAlive(clientWorld))
            {
                enabled = false;
                return;
            }

            if (registryQuery.IsEmpty || spaceshipRegistryQuery.IsEmpty || shipQuery.IsEmpty) return;

            PlayerInventory inventory = inventoryQuery.GetSingleton<LocalInventoryComponent>().Value;

            if (inventory == null) return;

            ItemRegistryAsset items = registryQuery.GetSingleton<ItemRegistryAssetComponent>().Value;
            SpaceshipRegistryAsset spaceships = spaceshipRegistryQuery.GetSingleton<SpaceshipRegistryAssetComponent>().Value;

            using NativeArray<Entity> ships = shipQuery.ToEntityArray(Allocator.Temp);
            SpaceshipId kind = entityManager.GetComponentData<SpaceshipKindComponent>(ships[0]).Value;
            ItemId turret = entityManager.GetComponentData<EquippedTurretComponent>(ships[0]).Value;
            DynamicBuffer<AutoTurretElement> autoTurrets = entityManager.GetBuffer<AutoTurretElement>(ships[0], true);
            ItemAsset powerCell = items.Find(ItemId.PowerCell);

            // 조종 포탑 총구가 없는 기종은 모든 칸이 자동 포탑 칸
            bool hasMainTurret = entityManager.GetBuffer<TurretElement>(ships[0], true).Length > 0;
            bool autoSelected = TurretEquip.AutoTurretIndex(inventory_win.SelectedSlot, hasMainTurret) >= 0;
            ItemId selected = ItemId.None;
            turretIcons.Clear();

            for (int i = 0; i < (hasMainTurret ? 1 : 0) + autoTurrets.Length; i++)
            {
                int auto = TurretEquip.AutoTurretIndex(i, hasMainTurret);
                ItemId slotTurret = auto < 0 ? turret : autoTurrets[auto].Turret;
                ItemAsset slotItem = items.Find(slotTurret);
                turretIcons.Add(slotItem != null ? slotItem.Sprite : null);

                if (i == inventory_win.SelectedSlot) selected = slotTurret;
            }

            inventory_win.Refresh(inventory, items, spaceships, spaceships.Find(kind), turretIcons, autoSelected);

            if (stateM.CurrentToString == stateSpaceship) spaceship_win.Refresh(inventory, kind);
            if (stateM.CurrentToString == stateTurret) turret_win.Refresh(inventory, selected, autoSelected);

            // 강화 사양이 없는 기체는 강화 창을 비움
            if (!entityManager.HasBuffer<UpgradeElement>(ships[0])) return;

            upgrade_win.Refresh(
                entityManager.GetBuffer<UpgradeElement>(ships[0], true),
                entityManager.GetBuffer<UpgradeLevelElement>(ships[0], true),
                inventory.Count(ItemId.PowerCell),
                powerCell != null ? powerCell.Sprite : null);
        }

        /// <summary>
        /// <see cref="WindowUpgrade"/>의 능력치 줄을 눌렀을 때 호출됨.
        /// </summary>
        private void OnUpgradePress(UpgradeStat _stat)
        {
            if (!ClientWorldScope.IsAlive(clientWorld)) return;

            Entity rpcEntity = entityManager.CreateEntity(typeof(UpgradeRPC), typeof(SendRpcCommandRequest));
            entityManager.SetComponentData(rpcEntity, new UpgradeRPC { Stat = _stat });
        }

        /// <summary>
        /// <see cref="WindowSpaceshipSpec"/>의 장착 버튼을 눌렀을 때 호출됨. 바꿔 타면 사양 창을 닫음.
        /// </summary>
        private void OnSpaceshipEquip()
        {
            if (!ClientWorldScope.IsAlive(clientWorld)) return;

            Entity rpcEntity = entityManager.CreateEntity(typeof(SpaceshipPurchaseRPC), typeof(SendRpcCommandRequest));
            entityManager.SetComponentData(rpcEntity, new SpaceshipPurchaseRPC { Spaceship = spaceship_win.Spaceship });

            OnSpecClosed();
        }

        /// <summary>
        /// <see cref="WindowTurretSpec"/>의 장착 버튼을 눌렀을 때 호출됨. 잠금 해제가 필요하면 서버가 재료를 씀. 사양 창을 닫음.
        /// </summary>
        private void OnTurretEquip()
        {
            if (!ClientWorldScope.IsAlive(clientWorld)) return;

            Entity rpcEntity = entityManager.CreateEntity(typeof(TurretEquipRPC), typeof(SendRpcCommandRequest));
            entityManager.SetComponentData(rpcEntity, new TurretEquipRPC
            {
                Slot   = (byte)inventory_win.SelectedSlot,
                Turret = turret_win.Turret,
            });

            OnSpecClosed();
        }

        private void OnSpecClosed() => stateM.Change(stateInventory);
    }
}
