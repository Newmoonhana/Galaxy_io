using System.Text;
using TMPro;
using UnityEngine;

namespace GalaxyIO.Scripts.Managed.UI
{
    /// <summary>
    /// IP 주소만 입력받는 입력창. 숫자가 아닌 글자를 버리고 옥텟마다 점을 끼워 넣음.
    /// </summary>
    [RequireComponent(typeof(TMP_InputField))]
    public sealed class IpAddressField : MonoBehaviour
    {
        // nnn.nnn.nnn.nnn
        private const int OctetCount = 4;

        private const int OctetDigits = 3;

        private const int MaxLength = OctetCount * OctetDigits + (OctetCount - 1);

        private TMP_InputField field;

        /// <summary>
        /// 입력된 주소 문자열.
        /// </summary>
        public string Address => field.text;

        private void Awake()
        {
            field = GetComponent<TMP_InputField>();
            field.characterLimit = MaxLength;
            field.onValueChanged.AddListener(OnValueChanged);
        }

        /// <summary>
        /// 주소를 채워 넣음.
        /// </summary>
        public void Fill(string address)
        {
            field.text = Format(address);
        }

        private void OnValueChanged(string text)
        {
            string formatted = Format(text);

            // 다듬을 게 없으면 커서를 건드리지 않음. 중간을 고치는 조작이 방해받지 않음.
            if (formatted == text)
            {
                return;
            }

            field.SetTextWithoutNotify(formatted);
            field.caretPosition = formatted.Length;
        }

        /// <summary>
        /// 숫자만 남기고 세 자리마다 점을 끼워 nnn.nnn.nnn.nnn 형태로 만듦.
        /// </summary>
        private static string Format(string text)
        {
            var builder = new StringBuilder(MaxLength);
            int octet = 0;
            int digits = 0;

            foreach (char c in text)
            {
                if (c == '.')
                {
                    // 빈 옥텟 앞의 점과 마지막 옥텟 뒤의 점은 버림.
                    if (digits == 0 || octet == OctetCount - 1)
                    {
                        continue;
                    }

                    builder.Append('.');
                    octet++;
                    digits = 0;
                    continue;
                }

                if (c < '0' || c > '9')
                {
                    continue;
                }

                // 옥텟이 꽉 차면 점을 넣고 다음 옥텟으로 넘김.
                if (digits == OctetDigits)
                {
                    if (octet == OctetCount - 1)
                    {
                        break;
                    }

                    builder.Append('.');
                    octet++;
                    digits = 0;
                }

                builder.Append(c);
                digits++;
            }

            return builder.ToString();
        }
    }
}
