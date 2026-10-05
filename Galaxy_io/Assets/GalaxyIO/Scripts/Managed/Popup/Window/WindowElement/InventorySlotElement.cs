using Newmoonhana.Runtime.Tools.PopupUI.Window;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace GalaxyIO.Tools.PopupUI.Window
{
    /// <summary>
    /// 인벤토리 한 칸. 베이스 표기에 개수 / 설계도 / 집어 든 표시를 덧붙임.
    /// </summary>
    public sealed class InventorySlotElement : SlotElement
    {
        [Tooltip("설계도 결과물의 작은 아이콘(좌측 상단). 설계도일 때만 보임")]
        [SerializeField] private Image badge_img;

        [Tooltip("우측 하단 개수. 2개 이상일 때만 보임")]
        [SerializeField] private TextMeshProUGUI count_tmp;

        [Tooltip("빈 장비 칸에 띄우는 칸 종류")]
        [SerializeField] private TextMeshProUGUI type_tmp;

        [Tooltip("집어 든 칸의 아이콘 색")]
        [SerializeField] private Color held_color = new Color(1.0F, 1.0F, 1.0F, 0.3F);

        public bool HasItem { get; private set; }

        public bool IsBlueprint { get; private set; }

        public void SetSlot(SlotView _view, Sprite _badge, int _count, string _typeLabel, bool _isHeld)
        {
            SetView(_view);

            HasItem = _view.Icon != null;
            IsBlueprint = _badge != null;

            SetIcon(badge_img, _badge);

            if (icon_img != null) icon_img.color = _isHeld ? held_color : Color.white;
            if (count_tmp != null) count_tmp.text = _count > 1 ? _count.ToString() : string.Empty;
            if (type_tmp != null) type_tmp.text = _typeLabel;

            //빈 칸도 내려놓을 대상이라 항상 누를 수 있게 둠
            Btn.interactable = true;
        }
    }
}
