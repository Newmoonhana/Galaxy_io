using TMPro;
using UnityEngine;

namespace GalaxyIO.Scripts.Managed.UI
{
    /// <summary>
    /// 말풍선 프리팹 하나의 뷰. 글자 크기에 맞춰 상자를 늘림. 피벗은 꼬리 끝.
    /// </summary>
    public sealed class ChatBubbleView : MonoBehaviour
    {
        [SerializeField]
        [Tooltip("리치 텍스트를 꺼 둬야 함. 가운데 정렬, 줄바꿈 사용.")]
        private TMP_Text text;

        [SerializeField]
        [Tooltip("Sliced 상자. 글자 크기 + 여백으로 늘어남.")]
        private SpriteRenderer box;

        [SerializeField]
        [Tooltip("상자 아래에 붙는 꼬리. 피벗은 가운데.")]
        private SpriteRenderer tail;

        [SerializeField]
        [Tooltip("글자 최대 너비. 넘치면 줄바꿈함.")]
        [Min(0.1F)]
        private float maxWidth = 6.0F;

        [SerializeField]
        [Tooltip("글자와 상자 테두리 사이 여백")]
        private Vector2 padding = new Vector2(0.2F, 0.1F);

        public void Set(string message, Color color)
        {
            text.text = message;
            text.color = color;

            Vector2 size = text.GetPreferredValues(message, maxWidth, 0.0F);
            size.x = Mathf.Min(size.x, maxWidth);
            text.rectTransform.sizeDelta = size;

            Vector2 boxSize = size + padding * 2.0F;
            float tailHeight = tail.sprite.bounds.size.y * tail.transform.localScale.y;
            var center = new Vector3(0.0F, tailHeight + boxSize.y * 0.5F, 0.0F);

            box.size = boxSize;
            box.transform.localPosition = center;
            text.transform.localPosition = center;
            tail.transform.localPosition = new Vector3(0.0F, tailHeight * 0.5F, 0.0F);
        }
    }
}