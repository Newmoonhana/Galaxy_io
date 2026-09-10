using GalaxyIO.JWServer.Runtime.DTO;
using GalaxyIO.JWServer.Runtime.Login;
using GalaxyIO.JWServer.Runtime.Message;
using TMPro;
using UnityEngine;

namespace GalaxyIO.Tools.PopupUI.Window
{
    // TODO -> 윈도우 만들면서 재검토 필요(현재 코드만 가져온 상태)
    public sealed class WindowLogin_Backend : MonoBehaviour
    {
        [SerializeField]
        private GameObject idInputField;

        [SerializeField]
        private GameObject pwInputField;

        [SerializeField]
        private GameObject mainScreen;

        [SerializeField]
        private GameObject loginScreen;

        [SerializeField]
        private GameObject signUpScreen;

        [SerializeField]
        private GameObject warningMessageScreen;

        public async void OnEnterPress()
        {
            TMP_InputField idTextBox = idInputField.GetComponent<TMP_InputField>();
            TMP_InputField pwTextBox = pwInputField.GetComponent<TMP_InputField>();
            string id = idTextBox.text;
            string pw = pwTextBox.text;

            //string _gameCode, int _memberType, string _actionType,
            //string _memberId, string _memberPw, string _memberName
            GameUserResponseDTO result = await LoginManager.LogIn(new GameUserRequestDTO(
                _gameCode: LoginManager.GAME_CODE,
                _actionType: "LOGIN",
                _memberType: -1,    //인풋 값 X
                _memberId: id,
                _memberPw: pw,
                _memberName: "[TEST]"));    //인풋 값 X

            if (ResponseMsgUtil.IsOk(result.responseMessage))
            {
                mainScreen.SetActive(true);
                loginScreen.SetActive(false);
                return;
            }

            // 경고창 활성화
            warningMessageScreen.SetActive(true);
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
            }
        }

        public void OnSkipPress()
        {

        }

        public void OnSignUpPress()
        {
            loginScreen.gameObject.SetActive(false);
            signUpScreen.gameObject.SetActive(true);
        }
    }
}
