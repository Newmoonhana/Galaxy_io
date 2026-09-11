using JWServer.Runtime.DTO;
using JWServer.Runtime.Login;
using JWServer.Runtime.Utils;
using Newmoonhana.Runtime.Tools.PopupUI.Window;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace GalaxyIO.Tools.PopupUI.Window
{
    public class WindowSignUp : WindowBase
    {
        [SerializeField] private Button enter_btn;
        [SerializeField] private Button cancel_btn;
        [SerializeField] private TMP_InputField id_if;
        [SerializeField] private TMP_InputField pw_if;
        [SerializeField] private TMP_InputField pwValidate_if;
        [SerializeField] private TMP_Text errorMsg_txt;

        protected override void OnEnable()
        {
            base.OnEnable();
            
            enter_btn.onClick.AddListener(OnSingUpBtnPress);
            cancel_btn.onClick.AddListener(OnCancelBtnPress);
        }

        protected override void OnDisable()
        {
            base.OnDisable();
            
            enter_btn.onClick.RemoveListener(OnSingUpBtnPress);
            cancel_btn.onClick.RemoveListener(OnCancelBtnPress);
        }

        private void OnSingUpBtnPress()
        {
            // TODO -> errorMsg_txt 부분 전체적으로 번역 적용 시 텍스트 넘겨주기 대신 키 값 넘겨주기 필요
            string id = id_if.text;
            if (string.IsNullOrEmpty(id))
            {
                errorMsg_txt.text = "Invalid ID!";
                return;
            }
            string pw = pw_if.text;
            if (string.IsNullOrEmpty(pw))
            {
                errorMsg_txt.text = "Invalid Password!";
                return;
            }
            string pwCheck = pwValidate_if.text;
            if (string.IsNullOrEmpty(pwCheck))
            {
                errorMsg_txt.text = "Invalid Password!";
                return;
            }
            // 패스워드, 패스워드 확인에 입력된 값이 다를 때
            if (!string.Equals(pw, pwCheck))
            {
                errorMsg_txt.text = "Validate Password doesn't match!";
                return;
            }
            
            Awaitable<GameUserResponseDTO> result = LoginManager.SignUp(
                new GameUserRequestDTO(
                    _gameCode: LoginManager.GAME_CODE,
                    _actionType: "JOIN",
                    _memberType: 0,
                    _memberId: id,
                    _memberPw: pw,
                    _memberName: "[TEMP] 테스트 유저"
                )
            );

            // TODO -> 기다리는 동안의 UI 처리(취소 등 다른 입력 금지, 비주얼 표기 등) 필요
            Awaitable<GameUserResponseDTO>.Awaiter awaiter = result.GetAwaiter();

            awaiter.OnCompleted(() => {
                // (아이디 중복 오류 등) 회원가입 실패 시
                errorMsg_txt.text = "[TEST] 실패!";

                // TODO -> 경고창 구현 후 연결 필요
                // 서버 자체 오류 시 경고창 팝업 호출

                // 경고창 활성화 (서버 관련 시?)
                /*var waringScreenBackend = messageScreen.GetComponentInChildren<WindowWarningMsg_Backend>();
                GameUserResponseDTO response = awaiter.GetResult();

                if (ResponseMsgUtil.IsOk(response.responseMessage))
                {
                    waringScreenBackend.SetTitleText("Sign Up Succeeded");
                    waringScreenBackend.SetMessageText("Log in with your ID and Password");
                }
                else
                {
                    // TODO -> 아이디가 이미 서버에 존재할 시 체크하는 지 확인 필요
                    waringScreenBackend.SetTitleText("Sign Up Failed");
                    waringScreenBackend.SetMessageText(response.responseMessage);
                }*/
            });
        }
        private static void OnCancelBtnPress()
        {
            // TODO -> 상위 팝업에서 상태 패턴 ㄱㄱ
            
        }
    }
}
