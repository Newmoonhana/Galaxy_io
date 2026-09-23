using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace GalaxyIO.Scripts.Managed.UI
{
    /// <summary>
    /// 킬 로그 한 줄의 뷰.
    /// </summary>
    public sealed class KillLogRow : MonoBehaviour
    {
        [SerializeField]
        [Tooltip("로컬 플레이어가 관련된 줄에만 색을 입히는 배경")]
        private Image background;

        [SerializeField]
        private TMP_Text label;

        public void Set(string text, bool highlight, Color highlightColor)
        {
            label.text = text;
            background.color = highlight ? highlightColor : Color.clear;
        }
    }
}
