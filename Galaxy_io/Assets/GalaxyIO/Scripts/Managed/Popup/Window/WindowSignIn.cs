using System;
using GalaxyIO.Tools.PopupUI.Popup;
using JWServer.Runtime.DTO;
using JWServer.Runtime.Login;
using JWServer.Runtime.Utils;
using Newmoonhana.Runtime.Tools.PopupUI.Window;
using Newmoonhana.Runtime.Tools.Scene;
using Newmoonhana.Runtime.Utils;
using Newmoonhana.Runtime.Utils.DEBUG;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.SceneManagement;
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
        
        private UnityEvent<string> _changeWindow;

        protected override void Awake()
        {
            base.Awake();
            _changeWindow = new UnityEvent<string>();
        }

        protected override void Start()
        {
            base.Start();
            id_if.text = string.Empty;
            pw_if.text = string.Empty;
            errorMsg_txt.text = string.Empty;
        }

        public override void Dispose()
        {
            base.Dispose();
            _changeWindow?.RemoveAllListeners();
            _changeWindow = null;
        }

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
        
        public void AddListener(UnityAction<string> _action)
        {
            _changeWindow.AddListener(_action);
        }

        private void OnSignInBtnPress()
        {
            if (!IsActive()) return;
            _ = RunSignIn();
        }
        private async Awaitable RunSignIn()
        {
            #region 서버 체크 전 조건 확인
            string id = id_if.text;
            if (string.IsNullOrEmpty(id))
            {
                WriteErrorMsgText("Invalid ID!");
                return;
            }
            string pw = pw_if.text;
            if (string.IsNullOrEmpty(pw))
            {
                WriteErrorMsgText("Invalid Password!");
                return;
            }
            #endregion

            // 서버 체크
            errorMsg_txt.text = string.Empty;
            // TODO -> 기다리는 동안의 UI 처리(취소 등 다른 입력 금지, 비주얼 표기 등) 필요
            Deactivate();
            GameUserResponseDTO result = await LoginManager.LogIn(new GameUserRequestDTO(
                _gameCode: LoginManager.GAME_CODE,
                _actionType: "LOGIN",
                _memberType: 0,
                _memberId: id,
                _memberPw: pw,
                _memberName: "[TEST]")); //인풋 값 X
            Activate();
            
            #region 로그인 성공
            if (ResponseMsgUtil.IsOk(result.responseMessage) || result.responseMessage is CommonMsg.ALREADY_ONLINE)
            {
                SceneManager.LoadScene("GalaxyIO_Main", LoadSceneMode.Single);
                return;
            }
            #endregion
            
            #region 로그인 실패
            if (ResponseMsgUtil.IsInvalidCredential(result.responseMessage))
                WriteErrorMsgText("Invalid ID or Password!");
            else
            {
                // 서버 자체 오류 시 경고창 팝업 호출
                const string key = "Popup_MsgBox";
                await AddressableAssetsManager.gameObject_lst.LoadData(key);
                GameObject winObj = Instantiate(AddressableAssetsManager.gameObject_lst.GetData(key).Data, transform.parent);
                AddressableAssetsManager.gameObject_lst.GetData(key).UnLoad();
                PopupMessageBox msgBox_popup = winObj?.GetComponent<PopupMessageBox>();
                if (msgBox_popup == null)
                {
                    errorMsg_txt.text = "something error...";
                    DebugLog.LogError($"프리팹 {key} 등록 안됨!");
                }
                
                msgBox_popup.SetWinText("Login Failed", result.responseMessage);
                msgBox_popup.Refresh();
            }
            return;

            // [TEMP] 번역 적용 시 I2방식으로 변경 필요
            // 실패 메세지 출력(비밀번호 틀릴 시 같은 굳이 창이 뜰 필요 없는 경우)
            void WriteErrorMsgText(string text)
            {
                errorMsg_txt.text = text;
            }
            #endregion
        }

        private void OnSignUpBtnPress()
        {
            if (!IsActive()) return;
            _changeWindow?.Invoke("signUp");
        }
    }
}