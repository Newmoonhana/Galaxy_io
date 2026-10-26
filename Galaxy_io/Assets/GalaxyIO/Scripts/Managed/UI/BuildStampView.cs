using Newmoonhana.Runtime.Utils;
using Newmoonhana.Runtime.Utils.Config;
using TMPro;
using UnityEngine;

namespace GalaxyIO.Scripts.Managed.UI
{
    /// <summary>
    /// 화면 좌측 하단 주석 표기. 번역 대상 아님.
    /// </summary>
    public class BuildStampView : MonoBehaviour
    {
        /// <summary>표기 묶음.</summary>
        public enum STAMPTYPE
        {
            LOBBY,  //개발 빌드 경고 + 저작권 + 버전
            INGAME  //모드 + 버전. 개발자 전용
        }

        //릴리스가 아님을 알리는 연두색
        private const string devColor = "#9BE564";

        [SerializeField]
        [Tooltip("표기 묶음. 로비 / 인게임")]
        private STAMPTYPE stamp_type = STAMPTYPE.LOBBY;

        [SerializeField]
        [Tooltip("표기를 쓸 대상. 보통 자기 자신")]
        private TMP_Text label_txt;

        private void Start()
        {
            label_txt.SetText(stamp_type == STAMPTYPE.LOBBY ? LobbyText() : IngameText());
        }

        //로비엔 채팅창이 없어 겹침이 없으므로 세 줄로 씀
        private static string LobbyText()
        {
            string dev = IsDevBuild() ? $"<color={devColor}>DEV: Is not Release Build!</color>\n" : string.Empty;
            return $"{dev}©Newmoonhana\nv{Application.version}";
        }

        //인게임 좌하단은 채팅창 아래 얇은 띠(HUD 좌표 y 0~19)만 비어 있음.
        //두 줄로 쓰면 채팅 입력줄에 가리므로 한 줄로 합침
        private static string IngameText()
            => $"DEV: {SO_NmhnConfigData.Instance.ModeType}  v{Application.version}";

        //PROD 모드가 아니거나 FINAL_BUILD 정의가 없으면 릴리스 빌드가 아님
        private static bool IsDevBuild()
        {
#if FINAL_BUILD
            return SO_NmhnConfigData.Instance.ModeType != MODETYPE.PROD;
#else
            return true;
#endif
        }
    }
}
