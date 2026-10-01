using GalaxyIO.DOTS.Components.Actor;
using GalaxyIO.Tools.PopupUI.Popup;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace GalaxyIO.Scripts.Managed.UI
{
    /// <summary>
    /// 인벤토리 한 칸의 뷰. 누르면 <see cref="InventoryScreen"/>에 알림.
    /// </summary>
    public sealed class InventorySlot : MonoBehaviour, IPointerClickHandler
    {
        [SerializeField]
        [Tooltip("우주선의 같은 번호 칸 종류와 맞출 것")]
        private SlotType type;

        [SerializeField]
        private Image icon;

        [SerializeField]
        [Tooltip("우측 하단 개수. 2개 이상일 때만 보임.")]
        private TMP_Text countLabel;

        [SerializeField]
        [Tooltip("빈 장비 칸에 띄우는 칸 종류")]
        private TMP_Text typeLabel;

        [SerializeField]
        [Tooltip("집어 든 칸의 아이콘 색")]
        private Color heldColor = new Color(1.0F, 1.0F, 1.0F, 0.3F);

        private InventoryScreen screen;
        private int index;

        public bool HasItem => icon.enabled;

        public void Bind(InventoryScreen owner, int slotIndex)
        {
            screen = owner;
            index = slotIndex;
        }

        public void Set(Sprite sprite, int count, bool held)
        {
            icon.sprite = sprite;
            icon.enabled = sprite != null;
            icon.color = held ? heldColor : Color.white;
            countLabel.text = count > 1 ? count.ToString() : string.Empty;
            typeLabel.text = sprite == null && type != SlotType.Common ? type.ToString() : string.Empty;
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            screen.OnSlotClick(index, eventData.button);
        }
    }
}
