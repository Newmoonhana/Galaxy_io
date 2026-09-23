using System;
using GalaxyIO.DOTS.Components.Actor;
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
        // 이미 통합 서버 회원인 경우 Win_SignUpJWServer로 넘어가는 버튼.
        [SerializeField] private Button jwServer_btn;
        [SerializeField] private TMP_InputField id_if;
        [SerializeField] private TMP_InputField pw_if;
        [SerializeField] private TMP_InputField pwValidate_if;
        [SerializeField] private TMP_InputField memberName_if;
        [SerializeField] private TMP_InputField nickName_if;
        [SerializeField] private TMP_Text accountErrorMsg_txt;
        [SerializeField] private TMP_Text memberNameErrorMsg_txt;
        [SerializeField] private TMP_Text nickNameErrorMsg_txt;

        // JW 서버의 memberName 최대 길이.
        private const int MemberNameMaxLength = 100;

        private UnityEvent<string> changeWindow;
        // 통합 서버에 같은 ID의 회원이 있을 때, 입력값을 Win_SignUpJWServer로 넘김.
        private UnityEvent<GameUserRequestDTO> toJWServerSignUp;

        protected override void Awake()
        {
            base.Awake();
            changeWindow = new UnityEvent<string>();
            toJWServerSignUp = new UnityEvent<GameUserRequestDTO>();
            memberName_if.characterLimit = MemberNameMaxLength;
            nickName_if.characterLimit = PlayerNameComponent.MaxLength;
        }

        protected override void Start()
        {
            base.Start();
            id_if.text = string.Empty;
            pw_if.text = string.Empty;
            pwValidate_if.text = string.Empty;
            memberName_if.text = string.Empty;
            nickName_if.text = string.Empty;
            ClearErrorMsg();
        }

        public override void Dispose()
        {
            base.Dispose();
            changeWindow?.RemoveAllListeners();
            changeWindow = null;
            toJWServerSignUp?.RemoveAllListeners();
            toJWServerSignUp = null;
        }
        
        protected override void OnEnable()
        {
            base.OnEnable();
            
            enter_btn.onClick.AddListener(OnSignUpBtnPress);
            cancel_btn.onClick.AddListener(OnCancelBtnPress);
            jwServer_btn.onClick.AddListener(OnJWServerBtnPress);
        }

        protected override void OnDisable()
        {
            base.OnDisable();
            
            enter_btn.onClick.RemoveListener(OnSignUpBtnPress);
            cancel_btn.onClick.RemoveListener(OnCancelBtnPress);
            jwServer_btn.onClick.RemoveListener(OnJWServerBtnPress);
        }
        
        public void AddListener(UnityAction<string> _action)
        {
            changeWindow.AddListener(_action);
        }

        /// <summary>
        /// 통합 ID 중복 시 알림 받을 리스너 등록.
        /// </summary>
        public void AddJWServerListener(UnityAction<GameUserRequestDTO> _action)
        {
            toJWServerSignUp.AddListener(_action);
        }
        
        private async Awaitable RunSignUp()
        {
            #region 서버 체크 전 조건 확인
            // [TEMP] errorMsg_txt 부분 전체적으로 번역 적용 시 텍스트 넘겨주기 대신 키 값 넘겨주기 필요
            ClearErrorMsg();
            string id = id_if.text;
            if (string.IsNullOrEmpty(id))
            {
                accountErrorMsg_txt.text = "Invalid ID!";
                return;
            }
            string pw = pw_if.text;
            if (string.IsNullOrEmpty(pw))
            {
                accountErrorMsg_txt.text = "Invalid Password!";
                return;
            }
            if (pw.Length < 6)
            {
                accountErrorMsg_txt.text = "패스워드는 6자리를 넘어야합니다";
                return;
            }
            
            string pwCheck = pwValidate_if.text;
            if (string.IsNullOrEmpty(pwCheck))
            {
                accountErrorMsg_txt.text = "Invalid Validate Password!";
                return;
            }
            // 패스워드, 패스워드 확인에 입력된 값이 다를 때
            if (!string.Equals(pw, pwCheck, StringComparison.Ordinal))
            {
                accountErrorMsg_txt.text = "Validate Password doesn't match!";
                return;
            }
            // 공백만 입력한 경우도 빈 이름으로 취급함.
            string memberName = memberName_if.text.Trim();
            if (string.IsNullOrEmpty(memberName))
            {
                memberNameErrorMsg_txt.text = "Invalid Name!";
                return;
            }
            string nickName = nickName_if.text.Trim();
            if (string.IsNullOrEmpty(nickName))
            {
                nickNameErrorMsg_txt.text = "Invalid NickName!";
                return;
            }
            #endregion
            
            //서버 체크
            Deactivate();
            GameUserRequestDTO signUpUserData = new GameUserRequestDTO(
                _gameCode: LoginManager.GAME_CODE,
                _actionType: "JOIN",
                _memberType: 0,
                _memberId: id,
                _memberPw: pw,
                _memberName: memberName,
                _nickName: nickName
            );
            GameUserResponseDTO result = await CommonProgress.Show(transform.parent, () => LoginManager.SignUp(signUpUserData));
            Activate();

            #region 로그인 성공
            if (ResponseMsgUtil.IsOk(result.responseMessage))
            {
                DebugLog.Log("회원가입 성공!");
                changeWindow?.Invoke("main");   // 회원가입 성공 시 로그인까지 완료된 상태
                return;
            }
            #endregion
            
            #region 회원가입 실패
            // 통합 서버에 같은 ID의 회원이 있음. 통합 ID 회원가입 창을 쓰라고 안내만 함.
            if (ResponseMsgUtil.IsInvalidCredential(result.responseMessage))
            {
                WriteErrorMsgText(accountErrorMsg_txt, "이미 JW 서버 아이디가 있습니다!");
                return;
            }
            
            // ---- 규칙 예외 처리 (아이디 중복 오류 등) ----
            // 이미 존재하는 회원 정보(아이디 비번 전부 동일)로 회원가입 시도 시
            if (result.responseMessage is RefreshTokenMsg.ACCOUNT_ALREADY_EXISTS)
            {
                WriteErrorMsgText(accountErrorMsg_txt, "이미 존재하는 회원입니다.");
                return;
            }
            if (result.responseMessage is RefreshTokenMsg.NICKNAME_ALREADY_EXISTS)
            {
                WriteErrorMsgText(nickNameErrorMsg_txt, "이미 사용 중인 닉네임입니다.");
                return;
            }
            // --------
            
            // ---- 서버 자체 오류 시 경고창 팝업 호출 ----
            const string key = "Common_MsgBox";
            await AddressableAssetsManager.gameObject_lst.LoadData(key);
            GameObject winObj = Instantiate(AddressableAssetsManager.gameObject_lst.GetData(key).Data, transform.parent);
            AddressableAssetsManager.gameObject_lst.GetData(key).UnLoad();
            CommonMessageBox msgBox = winObj?.GetComponent<CommonMessageBox>();
            if (msgBox == null)
            {
                accountErrorMsg_txt.text = "something error...";
                DebugLog.LogError($"프리팹 {key} 등록 안됨!");
            }

            msgBox.SetWinText("Sign Up Failed", result.responseMessage);
            msgBox.Refresh();
            // --------
                
            return;
            
            // [TEMP] 번역 적용 시 I2방식으로 변경 필요
            // 실패 메세지 출력(비밀번호 틀릴 시 같은 굳이 창이 뜰 필요 없는 경우)
            void WriteErrorMsgText(TMP_Text target, string text)
            {
                target.text = text;
            }
            #endregion
        }

        private void ClearErrorMsg()
        {
            accountErrorMsg_txt.text = string.Empty;
            memberNameErrorMsg_txt.text = string.Empty;
            nickNameErrorMsg_txt.text = string.Empty;
        }
        
        private void OnSignUpBtnPress()
        {
            if (!IsActive()) return;
            _ = RunSignUp();
        }
        
        private void OnCancelBtnPress()
        {
            if (!IsActive()) return;
            changeWindow?.Invoke("signIn");
        }
        
        /// <summary>
        /// 통합 ID 회원가입 창으로 이동. 입력 검증과 통합 회원 확인은 그 창이 담당하므로
        /// 여기서는 지금까지 입력한 값만 넘김.
        /// </summary>
        private void OnJWServerBtnPress()
        {
            if (!IsActive()) return;

            ClearErrorMsg();
            toJWServerSignUp?.Invoke(new GameUserRequestDTO(
                _gameCode: LoginManager.GAME_CODE,
                _actionType: "JOIN",
                _memberType: 0,
                _memberId: id_if.text,
                _memberPw: pw_if.text,
                _memberName: memberName_if.text.Trim(),
                _nickName: nickName_if.text.Trim()
            ));
        }
    }
}
