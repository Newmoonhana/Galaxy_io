using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace GalaxyIO.Scripts.Managed.UI
{
    /// <summary>
    /// 제작 화면의 재료 한 줄. 아이콘, 이름, 보유/필요 개수.
    /// </summary>
    public sealed class RecipeRow : MonoBehaviour
    {
        [SerializeField]
        private Image icon;

        [SerializeField]
        private TMP_Text label;

        [SerializeField]
        [Tooltip("재료가 모자랄 때의 글자 색")]
        private Color lackColor = new Color(0.95F, 0.3F, 0.3F);

        private string itemName;
        private int shownHave = -1;
        private int shownNeed = -1;

        public void SetItem(Sprite sprite, string localizedName)
        {
            icon.sprite = sprite;
            icon.enabled = sprite != null;
            itemName = localizedName;
            shownHave = -1;
        }

        // 개수가 바뀔 때만 글자를 다시 만듦.
        public void SetCount(int have, int need)
        {
            if (have == shownHave && need == shownNeed)
            {
                return;
            }

            shownHave = have;
            shownNeed = need;
            label.text = $"{itemName}: {have}/{need}";
            label.color = have >= need ? Color.white : lackColor;
        }
    }
}
