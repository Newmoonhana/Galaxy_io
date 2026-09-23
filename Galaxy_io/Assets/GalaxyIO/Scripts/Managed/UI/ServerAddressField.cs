using System.Text;
using TMPro;
using UnityEngine;

namespace GalaxyIO.Scripts.Managed.UI
{
    /// <summary>
    /// 서버 주소 입력창. IP 주소와 도메인을 받음. 대문자는 소문자로 바꾸고 [a-z0-9.-] 외 글자는 버림.
    /// </summary>
    [RequireComponent(typeof(TMP_InputField))]
    public sealed class ServerAddressField : MonoBehaviour
    {
        // 도메인 이름 최대 길이.
        private const int MaxLength = 253;

        private TMP_InputField field;

        /// <summary>
        /// 입력된 주소 문자열.
        /// </summary>
        public string Address => field.text;

        private void Awake()
        {
            field = GetComponent<TMP_InputField>();
            field.characterLimit = MaxLength;
            // 타이핑/붙여넣기 모두 글자 단위로 거름.
            field.onValidateInput = OnValidateInput;
        }

        /// <summary>
        /// 주소를 채워 넣음.
        /// </summary>
        public void Fill(string address)
        {
            field.text = Format(address);
        }

        private static char OnValidateInput(string text, int charIndex, char addedChar)
        {
            return Filter(addedChar);
        }

        /// <summary>
        /// 허용 글자는 소문자로 바꿔 돌려줌. 나머지는 '\0'(버림).
        /// </summary>
        private static char Filter(char c)
        {
            c = char.ToLowerInvariant(c);

            bool allowed = (c >= 'a' && c <= 'z') || (c >= '0' && c <= '9') || c == '.' || c == '-';

            return allowed ? c : '\0';
        }

        private static string Format(string text)
        {
            var builder = new StringBuilder(text.Length);

            foreach (char c in text)
            {
                char filtered = Filter(c);

                if (filtered != '\0')
                {
                    builder.Append(filtered);
                }
            }

            return builder.ToString();
        }
    }
}
