using System;
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
            changeWindow.AddListener(_action);
        }

        private void OnSignInBtnPress()
        {
            _ = RunSignIn();
        }
        private async Awaitable RunSignIn()
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

            // TODO -> 기다리는 동안의 UI 처리(취소 등 다른 입력 금지, 비주얼 표기 등) 필요
            GameUserResponseDTO result = await LoginManager.LogIn(new GameUserRequestDTO(
                _gameCode: LoginManager.GAME_CODE,
                _actionType: "LOGIN",
                _memberType: -1, //인풋 값 X
                _memberId: id,
                _memberPw: pw,
                _memberName: "[TEST]")); //인풋 값 X

            #region 로그인 실패
            if (!ResponseMsgUtil.IsOk(result.responseMessage))
            {
                // TODO -> 경고 메세지 및 팝업 호출 필요
                // 실패 메세지 출력(비밀번호 틀릴 시 같은 굳이 창이 뜰 필요 없는 경우)
                errorMsg_txt.text = "[TEST] 실패!";

                // 서버 자체 오류 시 경고창 호출
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
                if (ResponseMsgUtil.IsInvalidCredential(result.responseMessage))
                {
                    msgBox_win.SetTitleText("Login Failed");
                    msgBox_win.SetMessageText("Invalid ID or Password!");
                }
                else
                {
                    msgBox_win.SetTitleText("Login Failed");
                    msgBox_win.SetMessageText(result.responseMessage);
                }
                return;
            }
            #endregion

            #region 로그인 성공

            // TODO -> 씬 이동 이전 처리 작업?
            
            // TODO -> 싱글씬 이동
            await SceneStateManager.LoadSceneInst("S_Game", 10);

            #endregion
        }

        private void OnSignUpBtnPress()
        {
            changeWindow?.Invoke("signUp");
        }
    }
}