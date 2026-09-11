using System;
using GalaxyIO.JWServer.Runtime.DTO;
using GalaxyIO.JWServer.Runtime.Login;
using GalaxyIO.JWServer.Runtime.Message;
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
        [SerializeField] private TMP_InputField idInputField;
        [SerializeField] private TMP_InputField pwInputField;

        protected override void OnEnable()
        {
            base.OnEnable();
            signIn_btn.onClick.AddListener(OnSignInBtn);
            signUp_btn.onClick.AddListener(OnSignUpBtn);
        }

        protected override void OnDisable()
        {
            base.OnDisable();
            signIn_btn.onClick.RemoveListener(OnSignInBtn);
            signUp_btn.onClick.RemoveListener(OnSignUpBtn);
        }

        private async void OnSignInBtn()
        {
            string id = idInputField.text;
            string pw = pwInputField.text;
            
            GameUserResponseDTO result = await LoginManager.LogIn(new GameUserRequestDTO(
                _gameCode: LoginManager.GAME_CODE,
                _actionType: "LOGIN",
                _memberType: -1,    //인풋 값 X
                _memberId: id,
                _memberPw: pw,
                _memberName: "[TEST]"));    //인풋 값 X
            
            if (!ResponseMsgUtil.IsOk(result.responseMessage))
            {
                // TODO -> 경고 메세지 호출 방식 정해야함
                // 경고창 활성화
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
            }
            
            // TODO -> 상위 윈도우에서 상태 패턴 ㄱㄱ
            
        }
        private void OnSignUpBtn()
        {
            // TODO -> 회원가입 윈도우로 상태 패턴 변경
            
        }
    }
}