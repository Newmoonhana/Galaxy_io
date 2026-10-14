using Newmoonhana.Runtime.Tools.PopupUI.Window;
using UnityEngine;

namespace GalaxyIO.Tools.PopupUI.Window
{
    /// <summary>
    /// 재료 한 줄. 아이콘, 이름, 개수. 이름 칸이 없는 프리팹은 작은 가격표로 씀.
    /// </summary>
    public sealed class IngredientElement : SlotElement
    {
        [Tooltip("재료가 모자랄 때의 개수 글자 색")]
        [SerializeField] private Color lack_color = new Color(0.95F, 0.3F, 0.3F);

        private int shownHave = -1;
        private int shownNeed = -1;

        private int shownAmount = -1;
        private bool shownLacking;

        /// <summary>
        /// 재료 아이템. 재료가 바뀔 때만 호출함.
        /// </summary>
        public void SetItem(Sprite _icon, string _name)
        {
            shownHave = -1;
            shownNeed = -1;
            shownAmount = -1;

            SetView(new SlotView
            {
                Icon      = _icon,
                Name      = _name,
                Tooltip   = _name,
                IsEmpty   = _icon == null,
                IsEnabled = true,
            });
        }

        /// <summary>
        /// 보유 / 필요 개수. 개수가 바뀔 때만 글자를 다시 만듦.
        /// </summary>
        public void SetCount(int _have, int _need)
        {
            if (_have == shownHave && _need == shownNeed) return;

            shownHave = _have;
            shownNeed = _need;

            if (size_tmp == null) return;

            //베이스 SetView 가 이미 한 번 돈 뒤라 개수만 덮어씀
            size_tmp.text = $"{_have}/{_need}";
            size_tmp.enabled = true;
            size_tmp.color = _have >= _need ? Color.white : lack_color;
        }

        /// <summary>
        /// 개수 하나만 표기. 개수가 바뀔 때만 글자를 다시 만듦.
        /// </summary>
        public void SetAmount(int _amount, bool _isLacking)
        {
            if (_amount == shownAmount && _isLacking == shownLacking) return;

            shownAmount = _amount;
            shownLacking = _isLacking;

            if (size_tmp == null) return;

            size_tmp.text = _amount.ToString();
            size_tmp.enabled = true;
            size_tmp.color = _isLacking ? lack_color : Color.white;
        }
    }
}
