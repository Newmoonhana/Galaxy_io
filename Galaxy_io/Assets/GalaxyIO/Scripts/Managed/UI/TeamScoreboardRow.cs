using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace GalaxyIO.Scripts.Managed.UI
{
    /// <summary>
    /// 팀 점수판 한 줄의 뷰. 계급장, 이름, 처치, 어시스트, 사망, 응답 시간.
    /// </summary>
    public sealed class TeamScoreboardRow : MonoBehaviour
    {
        [SerializeField]
        private Image badgeImage;

        [SerializeField]
        [Tooltip("리치 텍스트를 꺼 둬야 함. 이름에 태그 삽입 방지.")]
        private TMP_Text nameLabel;

        [SerializeField]
        private TMP_Text killsLabel;

        [SerializeField]
        private TMP_Text assistsLabel;

        [SerializeField]
        private TMP_Text deathsLabel;

        [SerializeField]
        private TMP_Text pingLabel;

        public void Set(Sprite badge, string playerName, Color nameColor, int kills, int assists, int deaths, int ping)
        {
            badgeImage.sprite = badge;
            badgeImage.enabled = badge != null;
            nameLabel.text = playerName;
            nameLabel.color = nameColor;
            killsLabel.text = kills.ToString();
            assistsLabel.text = assists.ToString();
            deathsLabel.text = deaths.ToString();
            pingLabel.text = ping.ToString();
        }
    }
}
