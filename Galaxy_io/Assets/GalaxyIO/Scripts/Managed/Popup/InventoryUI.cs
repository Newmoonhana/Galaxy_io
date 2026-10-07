using System.Collections.Generic;
using GalaxyIO.DOTS.Authoring;
using GalaxyIO.DOTS.Authoring.Config;
using GalaxyIO.DOTS.Client.Components.Singleton;
using GalaxyIO.DOTS.Components.Message;
using GalaxyIO.DOTS.Components.Singleton;
using GalaxyIO.DOTS.Inventory;
using GalaxyIO.Tools.PopupUI.Window;
using Newmoonhana.Runtime.Tools.PopupUI.Popup;
using Newmoonhana.Runtime.Tools.PopupUI.Window;
using Newmoonhana.Runtime.Utils.Pattern;
using Unity.Entities;
using Unity.NetCode;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.Localization;
using UnityEngine.Localization.Settings;

namespace GalaxyIO.Tools.PopupUI.Popup
{
    /// <summary>
    /// 로컬 플레이어 우주선의 인벤토리 팝업. 칸 창과 제작 창을 관리함.
    /// <p/>
    /// 칸을 누르면(뗄 때가 아니라 누르는 순간) 그 묶음을 집어 듦. 집어 든 동안 다른 칸을 누르면 좌클릭은 전부, 우클릭은 1개를 내려놓고,
    /// 다른 아이템이 있으면 맞바꿈. 패널 밖을 누르면 같은 규칙으로 버림.
    /// 집어 든 묶음은 원래 칸에 그대로 있고, 서버에는 옮기기/버리기 요청만 보냄.
    /// 빈손으로 설계도를 우클릭하면 제작 창을 엶.
    /// </summary>
    public class InventoryUI : PopupBase
    {
        private const string stateInventory = "inventory";
        private const string stateCraft = "craft";

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
                win.Deactivate();
                win.Close();
            }
            public void Dispose()
            {
                win?.Dispose();
                win = null;
            }
        }

        /// <summary>
        /// 제작 창. 칸 창 위에 겹쳐 열리고 칸 창의 입력만 막음.
        /// </summary>
        private class CraftState : IState
        {
            private WindowInventory inv_win;
            private WindowCraft win;
            public CraftState(WindowInventory _invWin, WindowCraft _win)
            {
                inv_win = _invWin;
                win = _win;
                OnExit();
            }

            public void UpdateState(float elapsedTime) {}
            public void Render() {}
            public void OnEnter(object p_data)
            {
                // 직전 상태가 칸 창을 닫고 나가므로 다시 열어 둠. 입력만 막음
                inv_win.Open();
                inv_win.Deactivate();

                win.SetSlot((int)p_data);
                win.Open();
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
        private WindowCraft craft_win;

        private EntityManager entityManager;
        private EntityQuery inventoryQuery;
        private EntityQuery registryQuery;

        // 집어 든 칸 번호. 없으면 -1.
        private int heldSlot = -1;

        // 칸을 만들어 둔 인벤토리 길이. 바뀌면 다시 만듦.
        private int builtLength = -1;

        // 아이템 이름 조회 캐시. 칸 툴팁이 매 프레임 테이블을 뒤지지 않게 함.
        private readonly Dictionary<ItemId, string> names = new Dictionary<ItemId, string>();

        // 캐시한 쿼리가 속한 클라이언트 월드. 세션 종료 시 Dispose 되므로 매 프레임 확인함.
        private World clientWorld;

        protected override void Awake()
        {
            base.Awake();

            inventory_win = transform.GetComponentInChildren<WindowInventory>(true);
            craft_win = transform.GetComponentInChildren<WindowCraft>(true);
        }

        protected override void Start()
        {
            base.Start();

            inventory_win.OnSlotPressed += OnSlotPress;
            inventory_win.OnOutsidePressed += OnOutsidePress;
            craft_win.OnCraft += OnCraftPress;
            craft_win.OnClosed += OnCraftClosed;

            stateM.Add(stateInventory, new InventoryState(inventory_win));
            stateM.Add(stateCraft, new CraftState(inventory_win, craft_win));
            stateM.Change(stateInventory);

            LocalizationSettings.SelectedLocaleChanged += OnLocaleChanged;

            clientWorld = ClientServerBootstrap.ClientWorld;

            if (clientWorld == null)
            {
                enabled = false;
                return;
            }

            entityManager = clientWorld.EntityManager;
            inventoryQuery = entityManager.CreateEntityQuery(ComponentType.ReadOnly<LocalInventoryComponent>());
            registryQuery = entityManager.CreateEntityQuery(ComponentType.ReadOnly<ItemRegistryAssetComponent>());
        }

        protected override void OnDestroy()
        {
            base.OnDestroy();

            LocalizationSettings.SelectedLocaleChanged -= OnLocaleChanged;
            inventory_win = null;
            craft_win = null;
        }

        /// <summary>
        /// 생성 직후 호출. 닫기 처리는 생성한 쪽이 담당.
        /// </summary>
        public void AddListener(UnityAction _onExit)
        {
            inventory_win.AddListener(_onExit);
            craft_win.AddListener(_onExit);
        }

        private void LateUpdate()
        {
            // 세션을 나가면 클라이언트 월드가 Dispose 됨. 캐시한 쿼리를 더 쓰면 안 됨.
            if (!ClientWorldScope.IsAlive(clientWorld))
            {
                enabled = false;
                return;
            }

            if (registryQuery.IsEmpty) return;

            PlayerInventory inventory = inventoryQuery.GetSingleton<LocalInventoryComponent>().Value;
            ItemRegistryAsset registry = registryQuery.GetSingleton<ItemRegistryAssetComponent>().Value;

            if (inventory == null) return;

            if (builtLength != inventory.Length)
            {
                inventory_win.Build(inventory);
                builtLength = inventory.Length;
            }

            // 집어 든 칸이 비면 손도 비움. 전부 내려놓았거나 버린 경우.
            if (heldSlot >= inventory.Length || (heldSlot >= 0 && registry.Find(inventory[heldSlot].Id) == null))
                heldSlot = -1;

            RefreshSlots(inventory, registry);
            RefreshHeld(inventory, registry);

            if (stateM.CurrentToString == stateCraft) craft_win.Refresh(inventory, registry);
        }

        private void RefreshSlots(PlayerInventory _inventory, ItemRegistryAsset _registry)
        {
            for (int i = 0; i < inventory_win.Count && i < _inventory.Length; i++)
            {
                ItemSlot slot = _inventory[i];
                ItemAsset item = _registry.Find(slot.Id);

                // 설계도는 결과물 아이콘을 덧붙여 구분함.
                Sprite badge = item is BlueprintItemAsset blueprint && blueprint.Recipe != null && blueprint.Recipe.Result != null
                    ? blueprint.Recipe.Result.Sprite
                    : null;

                string name = item != null ? NameOf(item.Id) : string.Empty;

                inventory_win.SetSlot(i, new SlotView
                    {
                        Icon      = item != null ? item.Sprite : null,
                        Name      = name,
                        Tooltip   = name,
                        IsEmpty   = item == null,
                        IsEnabled = true,
                    },
                    badge,
                    slot.Count,
                    // 빈 장비 칸에만 칸 종류를 띄움
                    item == null && slot.Type != SlotType.Common ? slot.Type.ToString() : string.Empty,
                    i == heldSlot);
            }
        }

        private void RefreshHeld(PlayerInventory _inventory, ItemRegistryAsset _registry)
        {
            if (heldSlot < 0)
            {
                inventory_win.SetHeld(null, 0);
                return;
            }

            ItemSlot held = _inventory[heldSlot];
            inventory_win.SetHeld(_registry.Find(held.Id).Sprite, held.Count);
        }

        private string NameOf(ItemId _id)
        {
            if (names.TryGetValue(_id, out string name)) return name;

            name = L10n.Get($"Item/{_id}");
            names[_id] = name;
            return name;
        }

        private void OnLocaleChanged(Locale _locale) => names.Clear();

        /// <summary>
        /// <see cref="WindowInventory"/>의 칸을 눌렀을 때 호출됨.
        /// </summary>
        private void OnSlotPress(int _index, PointerEventData.InputButton _button)
        {
            if (heldSlot < 0)
            {
                InventorySlotElement slot = inventory_win.Element(_index);
                if (slot == null) return;

                if (_button == PointerEventData.InputButton.Right && slot.IsBlueprint)
                {
                    stateM.Change(stateCraft, _index);
                    return;
                }

                if (slot.HasItem) heldSlot = _index;

                return;
            }

            // 원래 칸을 다시 누르면 그대로 내려놓음.
            if (_index == heldSlot)
            {
                heldSlot = -1;
                return;
            }

            if (!ClientWorldScope.IsAlive(clientWorld)) return;

            Entity rpcEntity = entityManager.CreateEntity(typeof(InventoryMoveRPC), typeof(SendRpcCommandRequest));
            entityManager.SetComponentData(rpcEntity, new InventoryMoveRPC
            {
                From  = (byte)heldSlot,
                To    = (byte)_index,
                Count = _button == PointerEventData.InputButton.Right ? (byte)1 : byte.MaxValue,
            });
        }

        // 패널 밖을 누름. 집어 든 묶음을 버림.
        private void OnOutsidePress(PointerEventData.InputButton _button)
        {
            if (heldSlot < 0 || !ClientWorldScope.IsAlive(clientWorld)) return;

            Entity rpcEntity = entityManager.CreateEntity(typeof(InventoryDropRPC), typeof(SendRpcCommandRequest));
            entityManager.SetComponentData(rpcEntity, new InventoryDropRPC
            {
                Slot  = (byte)heldSlot,
                Count = _button == PointerEventData.InputButton.Right ? (byte)1 : byte.MaxValue,
            });
        }

        /// <summary>
        /// <see cref="WindowCraft"/>의 제작 버튼을 눌렀을 때 호출됨.
        /// </summary>
        private void OnCraftPress(int _blueprintSlot)
        {
            if (!ClientWorldScope.IsAlive(clientWorld)) return;

            Entity rpcEntity = entityManager.CreateEntity(typeof(CraftRPC), typeof(SendRpcCommandRequest));
            entityManager.SetComponentData(rpcEntity, new CraftRPC { Slot = (byte)_blueprintSlot });
        }

        private void OnCraftClosed() => stateM.Change(stateInventory);
    }
}
