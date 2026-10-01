using GalaxyIO.DOTS.Components.Actor;
using GalaxyIO.DOTS.Components.Message;
using GalaxyIO.DOTS.Components.Singleton;
using GalaxyIO.Scripts.Managed.UI;
using Newmoonhana.Runtime.Tools.PopupUI.Popup;
using TMPro;
using Unity.Collections;
using Unity.Entities;
using Unity.NetCode;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace GalaxyIO.Tools.PopupUI.Popup
{
    /// <summary>
    /// 로컬 플레이어 우주선의 인벤토리 화면. 씬에 상주하고 토글로 켜고 끔.
    /// <p/>
    /// 칸을 누르면(뗄 때가 아니라 누르는 순간) 그 묶음을 집어 듦. 집어 든 동안 다른 칸을 누르면 좌클릭은 전부, 우클릭은 1개를 내려놓고,
    /// 다른 아이템이 있으면 맞바꿈. 패널 밖을 누르면 같은 규칙으로 버림.
    /// 집어 든 묶음은 원래 칸에 그대로 있고, 서버에는 옮기기/버리기 요청만 보냄.
    /// 빈손으로 설계도를 우클릭하면 <see cref="CraftingScreen"/>을 엶.
    /// </summary>
    public class InventoryScreen : PopupBase, IPointerDownHandler
    {
        [SerializeField]
        [Tooltip("칸 번호 순. 우주선의 InventoryAuthoring 칸 구성과 같아야 함.")]
        private InventorySlot[] slots;

        [SerializeField]
        [Tooltip("이 영역 밖을 누르면 집어 든 아이템을 버림")]
        private RectTransform panel;

        [SerializeField]
        [Tooltip("집어 든 묶음을 보여주며 커서를 따라다니는 아이콘")]
        private Image heldIcon;

        [SerializeField]
        private TMP_Text heldCount;

        [SerializeField]
        private Button exitButton;

        [SerializeField]
        [Tooltip("인벤토리 위에 열리는 제작 화면. 꺼진 채로 둠.")]
        private CraftingScreen craftingScreen;

        private EntityManager entityManager;
        private EntityQuery inventoryQuery;
        private EntityQuery registryQuery;

        // 집어 든 칸 번호. 없으면 -1.
        private int heldSlot = -1;

        // 캐시한 쿼리가 속한 클라이언트 월드. 세션 종료 시 Dispose 되므로 매 프레임 확인함.
        private World clientWorld;

        protected override void Start()
        {
            base.Start();

            for (int i = 0; i < slots.Length; i++)
            {
                slots[i].Bind(this, i);
            }

            craftingScreen.Bind(this);

            clientWorld = ClientServerBootstrap.ClientWorld;

            if (clientWorld == null)
            {
                enabled = false;
                return;
            }

            entityManager = clientWorld.EntityManager;
            inventoryQuery = entityManager.CreateEntityQuery(
                ComponentType.ReadOnly<GhostOwnerIsLocal>(),
                ComponentType.ReadOnly<InventorySlotElement>()
            );
            registryQuery = entityManager.CreateEntityQuery(ComponentType.ReadOnly<ItemRegistryElement>());
        }

        // 제작 화면은 인벤토리를 열 때마다 닫힌 채로 시작함.
        protected override void OnEnable()
        {
            base.OnEnable();
            craftingScreen.gameObject.SetActive(false);
        }

        // 닫으면 집어 든 묶음을 놓음. 원래 칸에 그대로 있음.
        protected override void OnDisable()
        {
            base.OnDisable();
            heldSlot = -1;
            heldIcon.gameObject.SetActive(false);
        }

        private void LateUpdate()
        {
            // 세션을 나가면 클라이언트 월드가 Dispose 됨. 캐시한 쿼리를 더 쓰면 안 됨.
            if (!ClientWorldScope.IsAlive(clientWorld))
            {
                enabled = false;
                return;
            }

            if (inventoryQuery.IsEmpty || registryQuery.IsEmpty)
            {
                return;
            }

            using NativeArray<Entity> ships = inventoryQuery.ToEntityArray(Allocator.Temp);
            DynamicBuffer<InventorySlotElement> buffer = entityManager.GetBuffer<InventorySlotElement>(ships[0], true);
            Entity registryEntity = registryQuery.GetSingletonEntity();
            DynamicBuffer<ItemRegistryElement> registry = entityManager.GetBuffer<ItemRegistryElement>(registryEntity, true);

            // 집어 든 칸이 비면 손도 비움. 전부 내려놓았거나 버린 경우.
            if (heldSlot >= buffer.Length || (heldSlot >= 0 && !registry.Contains(buffer[heldSlot].Id)))
            {
                heldSlot = -1;
            }

            for (int i = 0; i < slots.Length && i < buffer.Length; i++)
            {
                ItemId id = buffer[i].Id;
                Sprite sprite = null;
                Sprite badge = null;

                if (registry.Contains(id))
                {
                    sprite = registry[(int)id].Sprite.Value;

                    // 설계도는 결과물 아이콘을 덧붙여 구분함.
                    ItemId result = registry[(int)id].CraftResult;
                    badge = registry.Contains(result) ? registry[(int)result].Sprite.Value : null;
                }

                slots[i].Set(sprite, badge, buffer[i].Count, i == heldSlot);
            }

            if (craftingScreen.gameObject.activeSelf)
            {
                craftingScreen.Refresh(
                    buffer,
                    registry,
                    entityManager.GetBuffer<RecipeIngredientElement>(registryEntity, true)
                );
            }

            heldIcon.gameObject.SetActive(heldSlot >= 0);

            if (heldSlot < 0 || Mouse.current == null)
            {
                return;
            }

            InventorySlotElement held = buffer[heldSlot];
            heldIcon.sprite = registry[(int)held.Id].Sprite.Value;
            heldCount.text = held.Count > 1 ? held.Count.ToString() : string.Empty;

            if (RectTransformUtility.ScreenPointToWorldPointInRectangle(
                    (RectTransform)heldIcon.transform.parent, Mouse.current.position.ReadValue(), cv.worldCamera, out Vector3 position))
            {
                heldIcon.rectTransform.position = position;
            }
        }

        /// <summary>
        /// 씬 시작 시 호출. 닫기 처리는 화면을 가진 쪽이 담당.
        /// </summary>
        public void AddListener(UnityAction _onExit)
        {
            exitButton.onClick.AddListener(_onExit);
        }

        /// <summary>
        /// <see cref="InventorySlot"/>을 눌렀을 때 호출됨.
        /// </summary>
        public void OnSlotPress(int index, PointerEventData.InputButton button)
        {
            if (heldSlot < 0)
            {
                if (button == PointerEventData.InputButton.Right && slots[index].IsBlueprint)
                {
                    craftingScreen.Open(index);
                    return;
                }

                if (slots[index].HasItem)
                {
                    heldSlot = index;
                }

                return;
            }

            // 원래 칸을 다시 누르면 그대로 내려놓음.
            if (index == heldSlot)
            {
                heldSlot = -1;
                return;
            }

            if (!ClientWorldScope.IsAlive(clientWorld))
            {
                return;
            }

            Entity rpcEntity = entityManager.CreateEntity(typeof(InventoryMoveRPC), typeof(SendRpcCommandRequest));
            entityManager.SetComponentData(rpcEntity, new InventoryMoveRPC
            {
                From  = (byte)heldSlot,
                To    = (byte)index,
                Count = button == PointerEventData.InputButton.Right ? (byte)1 : byte.MaxValue,
            });
        }

        /// <summary>
        /// <see cref="CraftingScreen"/>의 제작 버튼을 눌렀을 때 호출됨.
        /// </summary>
        public void OnCraftClick(int blueprintSlot)
        {
            if (!ClientWorldScope.IsAlive(clientWorld))
            {
                return;
            }

            Entity rpcEntity = entityManager.CreateEntity(typeof(CraftRPC), typeof(SendRpcCommandRequest));
            entityManager.SetComponentData(rpcEntity, new CraftRPC { Slot = (byte)blueprintSlot });
        }

        // 칸이 아닌 곳을 누름. 패널 밖이면 집어 든 묶음을 버림.
        public void OnPointerDown(PointerEventData eventData)
        {
            if (heldSlot < 0
                || RectTransformUtility.RectangleContainsScreenPoint(panel, eventData.position, eventData.pressEventCamera)
                || !ClientWorldScope.IsAlive(clientWorld))
            {
                return;
            }

            Entity rpcEntity = entityManager.CreateEntity(typeof(InventoryDropRPC), typeof(SendRpcCommandRequest));
            entityManager.SetComponentData(rpcEntity, new InventoryDropRPC
            {
                Slot  = (byte)heldSlot,
                Count = eventData.button == PointerEventData.InputButton.Right ? (byte)1 : byte.MaxValue,
            });
        }
    }
}
