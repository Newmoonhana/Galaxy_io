using TMPro;
using UnityEngine;

namespace GalaxyIO.Scripts.Managed.UI
{
    /// <summary>
    /// 점수판 한 줄의 뷰.
    /// </summary>
    public sealed class ScoreboardRow : MonoBehaviour
    {
        [SerializeField]
        private TMP_Text rankLabel;

        [SerializeField]
        [Tooltip("리치 텍스트를 꺼 둬야 함. 이름에 태그 삽입 방지.")]
        private TMP_Text nameLabel;

        [SerializeField]
        private TMP_Text scoreLabel;

        public void Set(int rank, string playerName, int score, Color nameColor)
        {
            rankLabel.text  = rank.ToString();
            nameLabel.text  = playerName;
            nameLabel.color = nameColor;
            scoreLabel.text = score.ToString();
        }
    }
}
