using System;
using System.Collections.Generic;
using GalaxyIO.DOTS.Inventory;
using Newmoonhana.Runtime.Tools.PopupUI.Window;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace GalaxyIO.Tools.PopupUI.Window
{
    /// <summary>
    /// 인벤토리 칸 창. 칸 종류별 목록과 커서를 따라다니는 아이콘을 가짐.
    /// </summary>
    public class WindowInventory : WindowBase, IPointerDownHandler
    {
        [Header("Slots")]
        [Tooltip("칸 종류 순(일반 -> 포탑 -> 폭탄). 칸 번호가 이 순서대로 이어짐")]
        [SerializeField] private WindowSlotList[] lists;

        [Header("Held")]
        [Tooltip("이 영역 밖을 누르면 집어 든 아이템을 버림")]
        [SerializeField] private RectTransform panel_rt;

        [Tooltip("집어 든 묶음을 보여주며 커서를 따라다니는 아이콘")]
        [SerializeField] private Image held_img;

        [SerializeField] private TextMeshProUGUI heldCount_tmp;

        [Header("Buttons")]
        [SerializeField] private Button exit_btn;

        /// <summary>칸 누름. 인자는 칸 번호와 누른 버튼.</summary>
        public event Action<int, PointerEventData.InputButton> OnSlotPressed;

        /// <summary>패널 밖 누름. 인자는 누른 버튼.</summary>
        public event Action<PointerEventData.InputButton> OnOutsidePressed;

        private UnityEvent _exit;

        // 칸 번호 순으로 펴 둔 칸. 목록 / 지역 번호 환산을 Build 에서 한 번만 하게 함
        private readonly List<InventorySlotElement> elements = new List<InventorySlotElement>();

        // 각 목록의 첫 칸 번호
        private int[] offsets;

        // 아이콘 좌표 환산용 캔버스
        private Canvas cv;

        public int Count => elements.Count;

        protected override void Awake()
        {
            base.Awake();

            _exit = new UnityEvent();
            cv = GetComponentInParent<Canvas>();
            offsets = new int[lists.Length];

            for (int i = 0; i < lists.Length; i++)
            {
                int listId = i;
                lists[i].OnPressed += (local, button) => RelaySlotPress(offsets[listId] + local, button);
            }
        }

        public override void Dispose()
        {
            base.Dispose();

            OnSlotPressed = null;
            OnOutsidePressed = null;
            _exit?.RemoveAllListeners();
            _exit = null;
            elements.Clear();
        }

        protected override void OnEnable()
        {
            base.OnEnable();
            exit_btn.onClick.AddListener(OnExitBtnPress);
        }

        protected override void OnDisable()
        {
            base.OnDisable();
            exit_btn.onClick.RemoveListener(OnExitBtnPress);
            held_img.gameObject.SetActive(false);
        }

        /// <summary>
        /// 생성 직후 호출. 닫기 처리는 생성한 쪽이 담당.
        /// </summary>
        public void AddListener(UnityAction _onExit) => _exit.AddListener(_onExit);

        /// <summary>
        /// 인벤토리 칸 구성대로 칸을 만듦. 칸 수가 바뀔 때만 호출함.
        /// </summary>
        public void Build(PlayerInventory _inventory)
        {
            elements.Clear();

            int cursor = 0;
            for (int i = 0; i < lists.Length; i++)
            {
                offsets[i] = cursor;

                int count = CountOf(_inventory, (SlotType)i);
                lists[i].SetCount(count);

                for (int local = 0; local < count; local++)
                    elements.Add(lists[i].Element(local) as InventorySlotElement);

                cursor += count;
            }
        }

        public InventorySlotElement Element(int _index)
            => _index >= 0 && _index < elements.Count ? elements[_index] : null;

        public void SetSlot(int _index, SlotView _view, Sprite _badge, int _count, string _typeLabel, bool _isHeld)
        {
            InventorySlotElement element = Element(_index);
            if (element == null) return;

            element.SetSlot(_view, _badge, _count, _typeLabel, _isHeld);
        }

        /// <summary>
        /// 집어 든 묶음 표시. 아이콘이 없으면 숨김.
        /// </summary>
        public void SetHeld(Sprite _icon, int _count)
        {
            held_img.gameObject.SetActive(_icon != null);
            if (_icon == null) return;

            held_img.sprite = _icon;
            heldCount_tmp.text = _count > 1 ? _count.ToString() : string.Empty;

            Follow();
        }

        // 칸 종류는 일반 -> 포탑 -> 폭탄 순으로 이어져 있음. lists 순서도 이와 같음
        private static int CountOf(PlayerInventory _inventory, SlotType _type)
        {
            int count = 0;

            for (int i = 0; i < _inventory.Length; i++)
                if (_inventory[i].Type == _type) count++;

            return count;
        }

        // 커서를 따라감. 캔버스 배율이 해상도마다 달라지므로 월드 좌표로 환산함
        private void Follow()
        {
            if (Mouse.current == null || cv == null) return;

            //GalaxyIO.Camera 네임스페이스가 있어 Camera 가 타입으로 안 잡힘
            UnityEngine.Camera cam = cv.renderMode == RenderMode.ScreenSpaceOverlay ? null : cv.worldCamera;

            if (RectTransformUtility.ScreenPointToWorldPointInRectangle(
                    (RectTransform)held_img.transform.parent, Mouse.current.position.ReadValue(), cam,
                    out Vector3 position))
                held_img.rectTransform.position = position;
        }

        private void RelaySlotPress(int _index, PointerEventData.InputButton _button)
        {
            if (!IsActive() || !IsOpen()) return;

            OnSlotPressed?.Invoke(_index, _button);
        }

        private void OnExitBtnPress()
        {
            if (!IsActive()) return;
            _exit?.Invoke();
        }

        // 칸이 아닌 곳을 누름. 패널 밖이면 집어 든 묶음을 버림
        public void OnPointerDown(PointerEventData _eventData)
        {
            if (!IsActive() || !IsOpen()) return;
            if (RectTransformUtility.RectangleContainsScreenPoint(panel_rt, _eventData.position, _eventData.pressEventCamera))
                return;

            OnOutsidePressed?.Invoke(_eventData.button);
        }
    }
}
