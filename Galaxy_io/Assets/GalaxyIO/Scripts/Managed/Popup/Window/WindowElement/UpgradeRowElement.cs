using Newmoonhana.Runtime.Tools.PopupUI.Window;
using UnityEngine;
using UnityEngine.UI;

namespace GalaxyIO.Tools.PopupUI.Window
{
    /// <summary>
    /// 강화 창의 능력치 한 줄. 이름, 단계(= 다음 강화에 드는 모듈 수), 강화 모듈 아이콘.
    /// 강화할 수 없으면 버튼이 잠겨 회색으로 보임.
    /// </summary>
    public sealed class UpgradeRowElement : SlotElement
    {
        [Tooltip("줄 배경. 줄 색을 어둡게 깔음")]
        [SerializeField] private Image back_img;

        [Tooltip("강화 모듈 아이콘")]
        [SerializeField] private Image module_img;

        [Tooltip("배경에 까는 줄 색의 밝기와 불투명도")]
        [SerializeField] private float backBrightness = 0.45F;
        [SerializeField] private float backAlpha = 0.6F;

        [Tooltip("강화할 수 없는 줄의 색. 줄 색마다 회색 밝기가 달라지지 않게 모든 줄이 같이 씀")]
        [SerializeField] private Color disabled_color = new Color(0.75F, 0.75F, 0.75F);

        private Color color = Color.white;

        private string shownName;
        private int shownLevel = -1;
        private bool shownEnabled;

        /// <summary>
        /// 줄 색. 줄을 만들 때 한 번만 호출함.
        /// </summary>
        public void SetColor(Color _color)
        {
            color = _color;
            shownLevel = -1;
        }

        /// <summary>
        /// 표시 값. 바뀔 때만 글자를 다시 만듦. 최대 단계면 단계 대신 MAX 를 띄움.
        /// </summary>
        public void SetUpgrade(string _name, Sprite _module, int _level, bool _isMax, bool _isEnabled)
        {
            SetIcon(module_img, _module);

            if (_name == shownName && _level == shownLevel && _isEnabled == shownEnabled) return;

            shownName = _name;
            shownLevel = _level;
            shownEnabled = _isEnabled;

            SetView(new SlotView
            {
                Name      = _name,
                Size      = _isMax ? "MAX" : _level.ToString(),
                IsEnabled = _isEnabled,
            });

            Color tint = _isEnabled ? color : disabled_color;

            if (name_tmp != null) name_tmp.color = tint;
            if (size_tmp != null) size_tmp.color = tint;

            if (back_img != null)
                back_img.color = new Color(
                    tint.r * backBrightness, tint.g * backBrightness, tint.b * backBrightness, backAlpha);
        }
    }
}
