using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace GalaxyIO.Scripts.Managed.UI
{
    /// <summary>
    /// 채팅창 탭 하나의 뷰.
    /// </summary>
    public sealed class ChatTab : MonoBehaviour
    {
        [SerializeField]
        private Button selectButton;

        [SerializeField]
        [Tooltip("귓속말 탭에만 보이는 닫기 버튼")]
        private Button closeButton;

        [SerializeField]
        private Image background;

        [SerializeField]
        [Tooltip("리치 텍스트를 꺼 둬야 함. 이름에 태그 삽입 방지.")]
        private TMP_Text label;

        [Header("Colors")]

        [SerializeField]
        private Color normalBackground = new Color(0.12F, 0.12F, 0.12F, 0.45F);

        [SerializeField]
        [Tooltip("보고 있는 탭의 배경")]
        private Color selectedBackground = new Color(0.3F, 0.3F, 0.3F, 0.8F);

        [SerializeField]
        private Color labelColor = Color.white;

        [SerializeField]
        [Tooltip("읽지 않은 귓속말이 있는 탭의 글자색")]
        private Color unreadColor = new Color(1.0F, 0.6F, 0.2F);

        /// <summary>
        /// 생성 직후 호출. 선택/닫기 처리는 생성한 쪽이 담당.
        /// </summary>
        public void Set(string title, bool closable, UnityAction onSelect, UnityAction onClose)
        {
            label.text = title;
            closeButton.gameObject.SetActive(closable);

            selectButton.onClick.AddListener(onSelect);
            closeButton.onClick.AddListener(onClose);
        }

        public void SetTitle(string title)
        {
            label.text = title;
        }

        public void SetState(bool selected, bool unread)
        {
            background.color = selected ? selectedBackground : normalBackground;
            label.color = unread ? unreadColor : labelColor;
        }
    }
}
