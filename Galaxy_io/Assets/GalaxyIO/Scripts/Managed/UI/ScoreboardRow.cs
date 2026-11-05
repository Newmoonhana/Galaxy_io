using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

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

        [SerializeField]
        [Tooltip("로컬 플레이어 줄에만 켜는 강조 배경")]
        private Image highlight;

        [SerializeField]
        [Tooltip("등장 / 퇴장 페이드 대상")]
        private CanvasGroup group;

        private RectTransform Rect => (RectTransform)transform;

        public void Set(int rank, string playerName, int score, Color nameColor, Color rankColor, bool isLocal)
        {
            rankLabel.text  = rank.ToString();
            rankLabel.color = rankColor;
            nameLabel.text  = playerName;
            nameLabel.color = nameColor;
            scoreLabel.text = score.ToString();
            highlight.enabled = isLocal;
        }

        /// <summary>
        /// 연출 없이 자리에 둠. 새로 꺼낸 줄에 씀.
        /// </summary>
        public void Place(float y)
        {
            Kill();
            Rect.anchoredPosition = new Vector2(Rect.anchoredPosition.x, y);
        }

        /// <summary>
        /// 순위가 바뀐 자리로 이동함.
        /// </summary>
        public void MoveTo(float y, float duration)
        {
            Rect.DOKill();
            Rect.DOAnchorPosY(y, duration).SetEase(Ease.OutQuad).SetLink(gameObject);
        }

        public void FadeIn(float duration)
        {
            group.DOKill();
            group.alpha = 0.0F;
            group.DOFade(1.0F, duration).SetLink(gameObject);
        }

        /// <summary>
        /// 흐려진 뒤 done 호출. 뷰가 풀에 반납함.
        /// </summary>
        public void FadeOut(float duration, TweenCallback done)
        {
            group.DOKill();
            group.DOFade(0.0F, duration).SetLink(gameObject).OnComplete(done);
        }

        public void Kill()
        {
            Rect.DOKill();
            group.DOKill();
            group.alpha = 1.0F;
        }
    }
}
