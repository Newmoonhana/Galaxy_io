using GalaxyIO.DOTS.Bootstrap;
using GalaxyIO.DOTS.Client.Components.Message;
using GalaxyIO.DOTS.Client.Networking;
using GalaxyIO.DOTS.Components.Actor;
using GalaxyIO.DOTS.Networking;
using GalaxyIO.Tools;
using GalaxyIO.Tools.PopupUI.Popup;
using Newmoonhana.Runtime.Tools.PopupUI.Window;
using Newmoonhana.Runtime.Utils;
using Newmoonhana.Runtime.Utils.DEBUG;
using TMPro;
using Unity.Entities;
using Unity.NetCode;
using Unity.Networking.Transport;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace GalaxyIO.Tools.PopupUI.Window
{
    /// <summary>
    /// 사설 서버를 직접 열고 접속하는 창.
    /// </summary>
    public class WindowServerHost : WindowBase
    {
        [SerializeField] private TMP_InputField playerName_input;
        [SerializeField] private TMP_Text nameWarning;
        [SerializeField] private Button hostButton;
        [SerializeField] private Button exitButton;

        private UnityEvent<string> _changeWindow;

        // 서버 열기·씬 로드를 기다리는 중.
        private bool busy;

        // 이름을 직접 입력할 수 있는 상태.
        private bool nameEditable;

        protected override void Awake()
        {
            base.Awake();
            _changeWindow = new UnityEvent<string>();
            playerName_input.characterLimit = PlayerNameComponent.MaxLength;
        }

        protected override void Start()
        {
            base.Start();

            nameWarning.gameObject.SetActive(false);
        }

        public override void Dispose()
        {
            base.Dispose();
            _changeWindow?.RemoveAllListeners();
            _changeWindow = null;
        }

        public override void Open()
        {
            base.Open();
            nameWarning.gameObject.SetActive(false);

            // 서버 열기는 항상 사설 서버.
            nameEditable = PlayerNameResolver.IsEditable(true);
            playerName_input.interactable = nameEditable;
            playerName_input.SetTextWithoutNotify(PlayerNameResolver.Suggest(true));
        }

        protected override void OnEnable()
        {
            base.OnEnable();
            hostButton.onClick.AddListener(OnPressHost);
            exitButton.onClick.AddListener(OnPressBack);
            playerName_input.onValueChanged.AddListener(OnNameChanged);
        }

        protected override void OnDisable()
        {
            base.OnDisable();
            hostButton.onClick.RemoveListener(OnPressHost);
            exitButton.onClick.RemoveListener(OnPressBack);
            playerName_input.onValueChanged.RemoveListener(OnNameChanged);
        }

        public void AddListener(UnityAction<string> _action)
        {
            _changeWindow.AddListener(_action);
        }

        /// <summary>
        /// 로비 메인으로 복귀.
        /// </summary>
        public void OnPressBack()
        {
            if (!IsActive() || busy) return;
            _changeWindow?.Invoke("main");
        }

        /// <summary>
        /// 서버를 열고 루프백으로 접속 요청.
        /// </summary>
        private void OnPressHost()
        {
            if (!IsActive() || busy) return;

            // 공백만 입력한 경우도 빈 이름으로 취급함.
            string playerName = playerName_input.text.Trim();

            if (playerName.Length == 0)
            {
                nameWarning.gameObject.SetActive(true);
                return;
            }

            nameWarning.gameObject.SetActive(false);
            _ = HostAsync(playerName);
        }

        /// <summary>
        /// 서버 월드를 띄우고 인게임 씬으로 들어감. 준비 동안 버튼을 잠금.
        /// </summary>
        private async Awaitable HostAsync(string playerName)
        {
            SetLocked(true);

            // 씬 로드·스트리밍·접속 동안 빈 화면이 보이지 않게 가림. 인게임에서 치움.
            _ = CommonProgress.ShowAcrossScenes();

            if (!await GameSession.HostAndPlay(playerName))
            {
                CommonProgress.HideAcrossScenes();
                SetLocked(false);
                await ShowHostFailed();
            }
        }

        // 편집 가능한 상태에서 입력한 이름만 세션에 남김.
        private void OnNameChanged(string value)
        {
            if (!nameEditable) return;

            PlayerNameResolver.Remember(value);
            nameWarning.gameObject.SetActive(false);
        }

        /// <summary>
        /// 대기 중 입력 잠금.
        /// </summary>
        private void SetLocked(bool value)
        {
            busy = value;
            hostButton.interactable = !value;
            playerName_input.interactable = !value && nameEditable;
        }

        /// <summary>
        /// 서버 열기 실패를 메시지 박스로 알림.
        /// </summary>
        private async Awaitable ShowHostFailed()
        {
            const string key = "Common_MsgBox";
            await AddressableAssetsManager.gameObject_lst.LoadData(key);
            GameObject winObj = Instantiate(AddressableAssetsManager.gameObject_lst.GetData(key).Data, transform.parent);
            AddressableAssetsManager.gameObject_lst.GetData(key).UnLoad();
            CommonMessageBox msgBox = winObj?.GetComponent<CommonMessageBox>();

            if (msgBox == null)
            {
                DebugLog.LogError($"프리팹 {key} 등록 안됨!");
                return;
            }

            msgBox.SetWinText(L10n.Get("ServerHost/FailedTitle"), L10n.Get("ServerHost/Failed"));
            msgBox.Refresh();
        }
    }
}
