using DG.Tweening;
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

        [SerializeField]
        [Tooltip("등장 / 퇴장 페이드 대상")]
        private CanvasGroup group;

        public void Set(string text, Color backgroundColor)
        {
            label.text = text;
            background.color = backgroundColor;
        }

        /// <summary>
        /// 커지면서 나타남.
        /// </summary>
        public void PlayIn(float duration)
        {
            Kill();
            group.alpha = 0.0F;
            transform.localScale = Vector3.one * 1.15F;
            group.DOFade(1.0F, duration).SetLink(gameObject);
            transform.DOScale(1.0F, duration).SetEase(Ease.OutBack).SetLink(gameObject);
        }

        /// <summary>
        /// 흐려지며 사라짐. 풀 반납은 뷰가 함.
        /// </summary>
        public void PlayOut(float duration)
        {
            group.DOKill();
            group.DOFade(0.0F, duration).SetLink(gameObject);
        }

        public void Kill()
        {
            group.DOKill();
            transform.DOKill();
            group.alpha = 1.0F;
            transform.localScale = Vector3.one;
        }
    }
}
