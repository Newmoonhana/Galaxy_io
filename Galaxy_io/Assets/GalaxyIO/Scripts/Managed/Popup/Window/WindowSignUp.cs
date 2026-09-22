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
        [SerializeField] private TMP_InputField id_if;
        [SerializeField] private TMP_InputField pw_if;
        [SerializeField] private TMP_InputField pwValidate_if;
        [SerializeField] private TMP_InputField memberName_if;
        [SerializeField] private TMP_InputField nickName_if;
        [SerializeField] private TMP_Text errorMsg_txt;

        // JW 서버의 memberName 최대 길이.
        private const int MemberNameMaxLength = 100;

        private UnityEvent<string> changeWindow;

        protected override void Awake()
        {
            base.Awake();
            changeWindow = new UnityEvent<string>();
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
            if (!IsActive()) return;
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
            /*// 아이디와 비번이 같을 시
            if (string.Equals(id, pw, StringComparison.Ordinal))
            {
                errorMsg_txt.text = "아이디와 비밀번호가 동일할 수 없습니다";
                return;
            }*/
            // 공백만 입력한 경우도 빈 이름으로 취급함.
            string memberName = memberName_if.text.Trim();
            if (string.IsNullOrEmpty(memberName))
            {
                errorMsg_txt.text = "Invalid Name!";
                return;
            }
            string nickName = nickName_if.text.Trim();
            if (string.IsNullOrEmpty(nickName))
            {
                errorMsg_txt.text = "Invalid NickName!";
                return;
            }
            #endregion
            
            //서버 체크
            errorMsg_txt.text = string.Empty;
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
            bool isDuplicateId = false;
            GameUserResponseDTO result = await CommonProgress.Show<GameUserResponseDTO>(transform.parent, async () =>
            {
                // 가입 전 ID 중복 확인
                MemberIdDupCheckResponseDTO dupCheck = await LoginManager.CheckMemberIdDup(id);
                isDuplicateId = ResponseMsgUtil.IsOk(dupCheck.responseMessage) && dupCheck.isDuplicate;
                return isDuplicateId ? default : await LoginManager.SignUp(signUpUserData);
            });
            Activate();

            if (isDuplicateId)
            {
                WriteErrorMsgText("이미 존재하는 회원 아이디입니다.");
                return;
            }

            #region 로그인 성공
            if (ResponseMsgUtil.IsOk(result.responseMessage))
            {
                DebugLog.Log("회원가입 성공!");
                changeWindow?.Invoke("main");   // 회원가입 성공 시 로그인까지 완료된 상태
                return;
            }
            #endregion
            
            #region 회원가입 실패
            // 규칙 예외 처리 (아이디 중복 오류 등)
            // 이미 존재하는 아이디로 회원가입 시도 시
            if (ResponseMsgUtil.IsInvalidCredential(result.responseMessage))
            {
                WriteErrorMsgText("이미 존재하는 회원 아이디입니다.");
                return;
            }
            // 이미 존재하는 회원 정보(아이디 비번 전부 동일)로 회원가입 시도 시
            if (result.responseMessage is RefreshTokenMsg.ACCOUNT_ALREADY_EXISTS)
            {
                WriteErrorMsgText("이미 존재하는 회원입니다.");
                return;
            }
            if (result.responseMessage is RefreshTokenMsg.NICKNAME_ALREADY_EXISTS)
            {
                WriteErrorMsgText("이미 사용 중인 닉네임입니다.");
                return;
            }
            
            // 서버 자체 오류 시 경고창 팝업 호출
            const string key = "Common_MsgBox";
            await AddressableAssetsManager.gameObject_lst.LoadData(key);
            GameObject winObj = Instantiate(AddressableAssetsManager.gameObject_lst.GetData(key).Data, transform.parent);
            AddressableAssetsManager.gameObject_lst.GetData(key).UnLoad();
            CommonMessageBox msgBox = winObj?.GetComponent<CommonMessageBox>();
            if (msgBox == null)
            {
                errorMsg_txt.text = "something error...";
                DebugLog.LogError($"프리팹 {key} 등록 안됨!");
            }

            msgBox.SetWinText("Sign Up Failed", result.responseMessage);
            msgBox.Refresh();
                
            return;
            
            // [TEMP] 번역 적용 시 I2방식으로 변경 필요
            // 실패 메세지 출력(비밀번호 틀릴 시 같은 굳이 창이 뜰 필요 없는 경우)
            void WriteErrorMsgText(string text)
            {
                errorMsg_txt.text = text;
            }
            #endregion
        }
        
        private void OnCancelBtnPress()
        {
            if (!IsActive()) return;
            changeWindow?.Invoke("signIn");
        }
    }
}
