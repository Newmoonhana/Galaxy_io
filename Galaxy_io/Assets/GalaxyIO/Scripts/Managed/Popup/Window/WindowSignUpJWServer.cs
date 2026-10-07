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
    /// <summary>
    /// 통합 서버에 이미 있는 회원 ID로 이 게임의 계정을 추가하는 창.
    /// </summary>
    public class WindowSignUpJWServer : WindowBase
    {
        [SerializeField] private Button enter_btn;
        [SerializeField] private Button cancel_btn;
        [SerializeField] private TMP_InputField id_if;
        [SerializeField] private TMP_InputField pw_if;
        [SerializeField] private TMP_InputField nickName_if;
        [SerializeField] private TMP_Text accountErrorMsg_txt;
        [SerializeField] private TMP_Text nickNameErrorMsg_txt;

        /// <summary>
        /// Win_SignUp에서 넘겨받은 회원 정보. 입력값은 이 창에서 다시 채움.
        /// 이름은 통합 회원 가입 때 등록한 값을 그대로 쓰므로 이 창에서 받지 않음.
        /// </summary>
        private GameUserRequestDTO userData;

        private UnityEvent<string> changeWindow;

        protected override void Awake()
        {
            base.Awake();
            changeWindow = new UnityEvent<string>();
            nickName_if.characterLimit = PlayerNameComponent.MaxLength;
        }

        protected override void Start()
        {
            base.Start();
            ResetInput();
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

        /// <summary>
        /// Win_SignUp에서 입력한 회원 정보를 넘겨받음. 창을 열기 직전에 호출.
        /// </summary>
        public void SetUserData(GameUserRequestDTO _userData)
        {
            userData = _userData;
            ResetInput();
        }

        /// <summary>
        /// Win_SignUp에서 넘어온 값으로 입력칸을 채움.
        /// </summary>
        private void ResetInput()
        {
            id_if.text = userData.memberId ?? string.Empty;
            pw_if.text = userData.memberPw ?? string.Empty;
            nickName_if.text = userData.nickName ?? string.Empty;
            ClearErrorMsg();
        }

        private void OnSignUpBtnPress()
        {
            if (!IsActive()) return;
            _ = RunSignUp();
        }
        /// <summary>
        /// 통합 회원 확인 후 이 게임의 계정을 추가.
        /// </summary>
        private async Awaitable RunSignUp()
        {
            #region 서버 체크 전 조건 확인
            ClearErrorMsg();
            string id = id_if.text;
            if (string.IsNullOrEmpty(id))
            {
                accountErrorMsg_txt.text = L10n.Get("SignUp/InvalidId");
                return;
            }
            string pw = pw_if.text;
            if (string.IsNullOrEmpty(pw))
            {
                accountErrorMsg_txt.text = L10n.Get("SignUp/InvalidPw");
                return;
            }
            if (pw.Length < 6)
            {
                accountErrorMsg_txt.text = L10n.Get("SignUp/PwTooShort");
                return;
            }
            // 공백만 입력한 경우도 빈 닉네임으로 취급함.
            string nickName = nickName_if.text.Trim();
            if (string.IsNullOrEmpty(nickName))
            {
                nickNameErrorMsg_txt.text = L10n.Get("SignUp/InvalidNickName");
                return;
            }
            #endregion

            userData.memberId = id;
            userData.memberPw = pw;
            userData.nickName = nickName;
            // 기존 회원이면 서버가 등록된 이름을 그대로 유지함. 다만 JOIN은 memberName이
            // 비어 있으면 400을 돌려주므로, Win_SignUp에서 받아온 값이 없으면 ID로 채워 보냄.
            if (string.IsNullOrEmpty(userData.memberName))
                userData.memberName = id;

            //서버 체크 — 통합 회원 확인이 끝나야 가입을 시도함
            Deactivate();
            GameUserResponseDTO result = await CommonProgress.Show<GameUserResponseDTO>(transform.parent, async () =>
            {
                MemberCheckResponseDTO check = await LoginManager.CheckMemberCredential(id, pw);
                if (!ResponseMsgUtil.IsOk(check.responseMessage))
                    return new GameUserResponseDTO(_responseMessage: check.responseMessage);

                return await LoginManager.SignUp(userData);
            });
            Activate();

            #region 회원가입 성공
            if (ResponseMsgUtil.IsOk(result.responseMessage))
            {
                DebugLog.Log("통합 ID로 회원가입 성공!");
                changeWindow?.Invoke("main");   // 회원가입 성공 시 로그인까지 완료된 상태
                return;
            }
            #endregion

            #region 회원가입 실패
            // 통합 서버에 없는 ID — 일반 회원가입 대상.
            if (ResponseMsgUtil.IsMemberNotFound(result.responseMessage))
            {
                accountErrorMsg_txt.text = L10n.Get("SignUpJW/NotIntegrated");
                return;
            }
            if (ResponseMsgUtil.IsInvalidCredential(result.responseMessage))
            {
                accountErrorMsg_txt.text = L10n.Get("SignUpJW/PwMismatch");
                return;
            }
            // 이 게임의 계정이 이미 있음. 회원가입이 아니라 로그인 대상.
            if (result.responseMessage is RefreshTokenMsg.ACCOUNT_ALREADY_EXISTS)
            {
                accountErrorMsg_txt.text = L10n.Get("SignUpJW/AlreadyJoined");
                return;
            }
            if (result.responseMessage is RefreshTokenMsg.NICKNAME_ALREADY_EXISTS)
            {
                nickNameErrorMsg_txt.text = L10n.Get("SignUpJW/NickNameTaken");
                return;
            }

            await ShowErrorMsgBox(L10n.Get("SignUp/FailedTitle"), result.responseMessage);
            #endregion
        }

        /// <summary>
        /// 서버 자체 오류용 경고창 호출.
        /// </summary>
        private async Awaitable ShowErrorMsgBox(string title, string responseMessage)
        {
            const string key = "Common_MsgBox";
            await AddressableAssetsManager.gameObject_lst.LoadData(key);
            GameObject winObj = Instantiate(AddressableAssetsManager.gameObject_lst.GetData(key).Data, transform.parent);
            AddressableAssetsManager.gameObject_lst.GetData(key).UnLoad();
            CommonMessageBox msgBox = winObj?.GetComponent<CommonMessageBox>();
            if (msgBox == null)
            {
                accountErrorMsg_txt.text = L10n.Get("Common/UnknownError");
                DebugLog.LogError($"프리팹 {key} 등록 안됨!");
                return;
            }

            msgBox.SetWinText(title, responseMessage);
            msgBox.Refresh();
        }

        private void ClearErrorMsg()
        {
            accountErrorMsg_txt.text = string.Empty;
            nickNameErrorMsg_txt.text = string.Empty;
        }

        private void OnCancelBtnPress()
        {
            if (!IsActive()) return;
            changeWindow?.Invoke("signUp");
        }

        /// <summary>취소 버튼과 같은 처리. ESC 용.</summary>
        public void Cancel() => OnCancelBtnPress();
    }
}
