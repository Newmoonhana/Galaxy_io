using GalaxyIO.DOTS.Bootstrap;
using GalaxyIO.DOTS.Components.Actor;
using GalaxyIO.Scripts.Managed.UI;
using GalaxyIO.Tools;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace GalaxyIO.Tools.PopupUI.Window
{
    /// <summary>
    /// 사설 서버 접속 창. 주소를 직접 입력하고, 비로그인이면 이름도 직접 정함.
    /// </summary>
    public class WindowGameStartPrivate : WindowGameStartBase
    {
        [SerializeField] private ServerAddressField addressField;
        [SerializeField] private TMP_InputField playerName_input;
        [SerializeField] private TMP_Text nameWarning;
        [SerializeField] private Button loopbackButton;

        // 이름을 직접 입력할 수 있는 상태.
        private bool nameEditable;

        protected override string Address => addressField.Address;

        protected override void Awake()
        {
            base.Awake();
            playerName_input.characterLimit = PlayerNameComponent.MaxLength;
        }

        protected override void Start()
        {
            base.Start();
            nameWarning.gameObject.SetActive(false);
        }

        public override void Open()
        {
            base.Open();
            nameWarning.gameObject.SetActive(false);
            RefreshName();
        }

        protected override void OnEnable()
        {
            base.OnEnable();
            loopbackButton.onClick.AddListener(OnPressLoopback);
            playerName_input.onValueChanged.AddListener(OnNameChanged);
        }

        protected override void OnDisable()
        {
            base.OnDisable();
            loopbackButton.onClick.RemoveListener(OnPressLoopback);
            playerName_input.onValueChanged.RemoveListener(OnNameChanged);
        }

        protected override void OnLock(bool value)
        {
            loopbackButton.interactable = !value;
            playerName_input.interactable = !value && nameEditable;
        }

        protected override string TakePlayerName()
        {
            // 공백만 입력한 경우도 빈 이름으로 취급함.
            string requestPlayerName = playerName_input.text.Trim();

            if (requestPlayerName.Length == 0)
            {
                nameWarning.gameObject.SetActive(true);
                return null;
            }

            nameWarning.gameObject.SetActive(false);
            return requestPlayerName;
        }

        /// <summary>
        /// 루프백 주소를 입력창에 채움. 접속은 하지 않음.
        /// </summary>
        private void OnPressLoopback()
        {
            if (!IsActive() || busy) return;

            addressField.Fill(NetcodeBootstrap.LoopbackAddress);
            warning.gameObject.SetActive(false);
        }

        /// <summary>
        /// 비로그인으로 접속할 때만 이름을 직접 입력할 수 있음.
        /// 그 외에는 계정 닉네임으로 고정함.
        /// </summary>
        private void RefreshName()
        {
            nameEditable = PlayerNameResolver.IsEditable(true);
            playerName_input.interactable = nameEditable;
            playerName_input.SetTextWithoutNotify(PlayerNameResolver.Suggest(true));
        }

        // 편집 가능한 상태에서 입력한 이름만 세션에 남김.
        private void OnNameChanged(string value)
        {
            if (!nameEditable) return;

            PlayerNameResolver.Remember(value);
            nameWarning.gameObject.SetActive(false);
        }
    }
}
