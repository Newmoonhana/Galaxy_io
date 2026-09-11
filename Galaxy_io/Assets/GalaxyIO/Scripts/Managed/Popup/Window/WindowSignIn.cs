using System;
using JWServer.Runtime.DTO;
using JWServer.Runtime.Login;
using JWServer.Runtime.Utils;
using Newmoonhana.Runtime.Tools.PopupUI.Window;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace GalaxyIO.Tools.PopupUI.Window
{
    public class WindowSignIn : WindowBase
    {
        [SerializeField] private Button signIn_btn;
        [SerializeField] private Button signUp_btn;
        [SerializeField] private TMP_InputField id_if;
        [SerializeField] private TMP_InputField pw_if;
        [SerializeField] private TMP_Text errorMsg_txt;

        protected override void OnEnable()
        {
            base.OnEnable();
            signIn_btn.onClick.AddListener(OnSignInBtnPress);
            signUp_btn.onClick.AddListener(OnSignUpBtnPress);
        }

        protected override void OnDisable()
        {
            base.OnDisable();
            signIn_btn.onClick.RemoveListener(OnSignInBtnPress);
            signUp_btn.onClick.RemoveListener(OnSignUpBtnPress);
        }

        private async void OnSignInBtnPress()
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

            GameUserResponseDTO result = await LoginManager.LogIn(new GameUserRequestDTO(
                _gameCode: LoginManager.GAME_CODE,
                _actionType: "LOGIN",
                _memberType: -1, //인풋 값 X
                _memberId: id,
                _memberPw: pw,
                _memberName: "[TEST]")); //인풋 값 X

            if (!ResponseMsgUtil.IsOk(result.responseMessage))
            {
                #region 로그인 실패

                // TODO -> 경고 메세지 및 팝업 호출 필요
                // 실패 메세지 출력(비밀번호 틀릴 시 같은 굳이 창이 뜰 필요 없는 경우)
                errorMsg_txt.text = "[TEST] 실패!";

                // 서버 자체 오류 시 경고창 호출
                /*warningMessageScreen.SetActive(true);
                var waringScreenBackend = warningMessageScreen.GetComponentInChildren<WindowWarningMsg_Backend>();

                if (ResponseMsgUtil.IsInvalidCredential(result.responseMessage))
                {
                    waringScreenBackend.SetTitleText("Login Failed");
                    waringScreenBackend.SetMessageText("Invalid ID or Password!");
                }
                else
                {
                    waringScreenBackend.SetTitleText("Login Failed");
                    waringScreenBackend.SetMessageText(result.responseMessage);
                }*/

                #endregion
            }

            #region 로그인 성공

            // TODO -> 씬 이동 이전 처리 작업?
            
            // TODO -> 싱글씬 이동

            #endregion
        }

        private static void OnSignUpBtnPress()
        {
            // TODO -> 상위 팝업에서 상태 패턴 ㄱㄱ SignUp으로
            
        }
    }
}