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
    public class WindowSignIn : WindowBase
    {
        [SerializeField] private Button signIn_btn;
        [SerializeField] private Button signUp_btn;
        [SerializeField] private Button cancel_btn;
        [SerializeField] private TMP_InputField id_if;
        [SerializeField] private TMP_InputField pw_if;
        [SerializeField] private TMP_Text errorMsg_txt;
        
        private UnityEvent<string> _changeWindow;
        // 로그인 성공을 알림. 다음에 표시할 창은 LobbyUI가 결정함.
        private UnityEvent _loginSuccess;

        protected override void Awake()
        {
            base.Awake();
            _changeWindow = new UnityEvent<string>();
            _loginSuccess = new UnityEvent();
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
            _loginSuccess?.RemoveAllListeners();
            _loginSuccess = null;
        }

        protected override void OnEnable()
        {
            base.OnEnable();
            signIn_btn.onClick.AddListener(OnSignInBtnPress);
            signUp_btn.onClick.AddListener(OnSignUpBtnPress);
            cancel_btn.onClick.AddListener(OnCancelBtnPress);
        }

        protected override void OnDisable()
        {
            base.OnDisable();
            signIn_btn.onClick.RemoveListener(OnSignInBtnPress);
            signUp_btn.onClick.RemoveListener(OnSignUpBtnPress);
            cancel_btn.onClick.RemoveListener(OnCancelBtnPress);
        }
        
        public void AddListener(UnityAction<string> _action)
        {
            _changeWindow.AddListener(_action);
        }

        /// <summary>
        /// 로그인 성공 시 알림 받을 리스너 등록.
        /// </summary>
        public void AddListener(UnityAction _action)
        {
            _loginSuccess.AddListener(_action);
        }

        private void OnSignInBtnPress()
        {
            if (!IsActive()) return;
            RunSignIn().Forget();
        }
        private async Awaitable RunSignIn()
        {
            #region 서버 체크 전 조건 확인
            string id = id_if.text;
            if (string.IsNullOrEmpty(id))
            {
                WriteErrorMsgText(L10n.Get("SignIn/InvalidId"));
                return;
            }
            string pw = pw_if.text;
            if (string.IsNullOrEmpty(pw))
            {
                WriteErrorMsgText(L10n.Get("SignIn/InvalidPw"));
                return;
            }
            #endregion

            // 서버 체크
            errorMsg_txt.text = string.Empty;
            Deactivate();
            GameUserResponseDTO result = await CommonProgress.Show(transform.parent, () => LoginManager.LogIn(new GameUserRequestDTO(
                _gameCode: LoginManager.GAME_CODE,
                _actionType: "LOGIN",
                _memberType: 0,
                _memberId: id,
                _memberPw: pw,
                _memberName: "[TEST]"))); //인풋 값 X
            Activate();
            
            #region 로그인 성공
            if (ResponseMsgUtil.IsOk(result.responseMessage) || result.responseMessage is CommonMsg.ALREADY_ONLINE)
            {
                _loginSuccess?.Invoke();
                return;
            }
            #endregion
            
            #region 로그인 실패
            if (ResponseMsgUtil.IsInvalidCredential(result.responseMessage))
                WriteErrorMsgText(L10n.Get("SignIn/InvalidCredential"));
            else
            {
                // 서버 자체 오류 시 경고창 팝업 호출
                CommonMessageBox msgBox = await CommonMessageBox.Open(transform.parent);
                if (msgBox == null)
                {
                    errorMsg_txt.text = L10n.Get("Common/UnknownError");
                    return;
                }

                msgBox.SetWinText(L10n.Get("SignIn/FailedTitle"), result.responseMessage);
                msgBox.Refresh();
            }
            return;

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

        private void OnCancelBtnPress()
        {
            if (!IsActive()) return;
            _changeWindow?.Invoke("main");
        }

        /// <summary>취소 버튼과 같은 처리. ESC 용.</summary>
        public void Cancel() => OnCancelBtnPress();
    }
}