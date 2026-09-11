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
            
            // TODO -> 기다리는 동안의 UI 처리(취소 등 다른 입력 금지, 비주얼 표기 등) 필요
            GameUserResponseDTO result = await LoginManager.SignUp(
                new GameUserRequestDTO(
                    _gameCode: LoginManager.GAME_CODE,
                    _actionType: "JOIN",
                    _memberType: 0,
                    _memberId: id,
                    _memberPw: pw,
                    _memberName: "[TEMP] 테스트 유저"
                )
            );

            #region 회원가입 실패
            if (!ResponseMsgUtil.IsOk(result.responseMessage))
            {
                // (아이디 중복 오류 등) 회원가입 실패 시
                errorMsg_txt.text = "[TEST] 실패!";

                // TODO -> 경고창 구현 후 연결 필요
                // 서버 자체 오류 시 경고창 팝업 호출
                const string key = "Popup_MsgBox";
                await AddressableAssetsManager.gameObject_lst.LoadData(key);
                WindowMsgBox msgBox_win = AddressableAssetsManager.gameObject_lst.GetData(key).Data?
                    .GetComponent<WindowMsgBox>();
                if (!msgBox_win)
                {
                    errorMsg_txt.text = "something error...";
                    DebugLog.LogError($"프리팹 {key} 등록 안됨!");
                    return;
                }
                
                // TODO -> 아이디가 이미 서버에 존재할 시 체크하는 지 확인 필요
                msgBox_win.SetTitleText("Sign Up Failed");
                msgBox_win.SetMessageText(result.responseMessage);
                
                return;
            }
            #endregion

            #region 로그인 성공
            // TODO -> 씬 변경 작업 필요
            errorMsg_txt.text = "[TEST] 성공!";
            #endregion
        }
        
        private void OnCancelBtnPress()
        {
            changeWindow?.Invoke("signIn");
        }
    }
}
