using System;
using GalaxyIO.Tools.PopupUI.Popup;
using JWServer.Runtime.DTO;
using JWServer.Runtime.Login;
using JWServer.Runtime.Utils;
using Newmoonhana.Runtime.Tools.PopupUI.Window;
using Newmoonhana.Runtime.Utils;
using Newmoonhana.Runtime.Utils.DEBUG;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
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

        private UnityEvent<string> changeWindow;

        protected override void Awake()
        {
            base.Awake();
            changeWindow = new UnityEvent<string>();
        }

        protected override void Start()
        {
            base.Start();
            errorMsg_txt.text = string.Empty;
        }

        public override void Dispose()
        {
            base.Dispose();
            changeWindow?.RemoveAllListeners();
            changeWindow = null;
        }
        
        protected override void OnEnable()
        {
            base.OnEnable();
            
            enter_btn.onClick.AddListener(OnSignUpBtnPress);
            cancel_btn.onClick.AddListener(OnCancelBtnPress);
        }

        protected override void OnDisable()
        {
            base.OnDisable();
            
            enter_btn.onClick.RemoveListener(OnSignUpBtnPress);
            cancel_btn.onClick.RemoveListener(OnCancelBtnPress);
        }
        
        public void AddListener(UnityAction<string> _action)
        {
            changeWindow.AddListener(_action);
        }

        private void OnSignUpBtnPress()
        {
            _ = RunSignUp();
        }
        private async Awaitable RunSignUp()
        {
            #region 서버 체크 전 조건 확인
            // [TEMP] errorMsg_txt 부분 전체적으로 번역 적용 시 텍스트 넘겨주기 대신 키 값 넘겨주기 필요
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
            if (pw.Length < 6)
            {
                errorMsg_txt.text = "패스워드는 6자리를 넘어야합니다";
                return;
            }
            
            string pwCheck = pwValidate_if.text;
            if (string.IsNullOrEmpty(pwCheck))
            {
                errorMsg_txt.text = "Invalid Validate Password!";
                return;
            }
            // 패스워드, 패스워드 확인에 입력된 값이 다를 때
            if (!string.Equals(pw, pwCheck, StringComparison.Ordinal))
            {
                errorMsg_txt.text = "Validate Password doesn't match!";
                return;
            }
            // 아이디와 비번이 같을 시
            if (string.Equals(id, pw, StringComparison.Ordinal))
            {
                errorMsg_txt.text = "아이디와 비밀번호가 동일할 수 없습니다";
                return;
            }
            #endregion
            
            //서버 체크
            errorMsg_txt.text = string.Empty;
            // TODO -> 기다리는 동안의 UI 처리(취소 등 다른 입력 금지, 비주얼 표기 등) 필요
            GameUserRequestDTO signUpUserData = new GameUserRequestDTO(
                _gameCode: LoginManager.GAME_CODE,
                _actionType: "JOIN",
                _memberType: 0,
                _memberId: id,
                _memberPw: pw,
                _memberName: "[TEMP] 테스트 유저"
            );
            GameUserResponseDTO result = await LoginManager.SignUp(signUpUserData);

            #region 로그인 성공
            if (ResponseMsgUtil.IsOk(result.responseMessage))
            {
                DebugLog.Log("[TEST] 회원가입 성공!");
                changeWindow?.Invoke("signIn");
                return;
            }
            #endregion
            
            #region 회원가입 실패
            // 규칙 예외 처리
            // (아이디 중복 오류 등) 회원가입 실패 시
            if (ResponseMsgUtil.IsInvalidCredential(result.responseMessage))
            {
                errorMsg_txt.text = "이미 존재하는 회원 아이디입니다.";
                return;
            }

            // TODO -> 경고창 구현 후 연결 필요
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
                return;
            }

            msgBox_popup.SetWinText("Sign Up Failed", result.responseMessage);
            msgBox_popup.Refresh();
                
            return;
            #endregion
        }
        
        private void OnCancelBtnPress()
        {
            changeWindow?.Invoke("signIn");
        }
    }
}
