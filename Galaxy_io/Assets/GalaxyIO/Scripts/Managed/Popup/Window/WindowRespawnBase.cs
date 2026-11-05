using System;
using GalaxyIO.DOTS.Client.Components.Singleton;
using GalaxyIO.DOTS.Components.Actor;
using GalaxyIO.Tools.PopupUI.Popup;
using Newmoonhana.Runtime.Tools.PopupUI.Window;
using TMPro;
using Unity.Entities;
using Unity.NetCode;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace GalaxyIO.Tools.PopupUI.Window
{
    /// <summary>
    /// 리스폰 창의 공통부. 나가기 버튼, 웹 전용 크레딧/설정 버튼과 이름 입력란.
    /// </summary>
    public abstract class WindowRespawnBase : WindowBase
    {
        [SerializeField] private Button quit_btn;

        [Tooltip("이름 입력란. 이름을 직접 정할 수 있을 때(웹 비로그인)만 보임.")]
        [SerializeField] private TMP_InputField playerName_input;

        [Header("Web")]
        [Tooltip("웹 빌드에서만 켜는 크레딧 버튼")]
        [SerializeField] private Button credit_btn;
        [Tooltip("웹 빌드에서만 켜는 설정 버튼")]
        [SerializeField] private Button settings_btn;

        private UnityEvent _quit;

        // 입력란 포커스를 PlayerStateComponent.NameFocused 로 알림. 글자 키가 게임 단축키로 쓰이지 않게 함.
        private World clientWorld;
        private EntityQuery playerStateQuery;
        private bool wasNameFocused;

        protected override void Awake()
        {
            base.Awake();
            _quit = new UnityEvent();

            playerName_input.characterLimit = PlayerNameComponent.MaxLength;
            playerName_input.gameObject.SetActive(PlayerNameResolver.IsEditable(false));
            playerName_input.SetTextWithoutNotify(PlayerNameResolver.Suggest(false));

            clientWorld = ClientServerBootstrap.ClientWorld;

            if (clientWorld != null)
            {
                playerStateQuery = clientWorld.EntityManager.CreateEntityQuery(ComponentType.ReadWrite<PlayerStateComponent>());
            }

#if UNITY_WEBGL && !UNITY_EDITOR
            quit_btn.gameObject.SetActive(false);
#else
            credit_btn.gameObject.SetActive(false);
            settings_btn.gameObject.SetActive(false);
#endif
        }

        public override void Dispose()
        {
            base.Dispose();
            _quit?.RemoveAllListeners();
            _quit = null;
        }

        protected override void OnEnable()
        {
            base.OnEnable();
            quit_btn.onClick.AddListener(OnQuitBtnPress);
            credit_btn.onClick.AddListener(OnCreditBtnPress);
            settings_btn.onClick.AddListener(OnSettingsBtnPress);
        }

        protected override void OnDisable()
        {
            base.OnDisable();
            quit_btn.onClick.RemoveListener(OnQuitBtnPress);
            credit_btn.onClick.RemoveListener(OnCreditBtnPress);
            settings_btn.onClick.RemoveListener(OnSettingsBtnPress);

            SetNameFocused(false);
        }

        protected override void OnDestroy()
        {
            SetNameFocused(false);
            base.OnDestroy();
        }

        private void LateUpdate()
        {
            // 입력을 끝낸 Enter 가 같은 프레임에 채팅을 열지 않도록 포커스가 풀린 프레임까지 켜 둠.
            bool focused = playerName_input.isFocused;
            SetNameFocused(focused || wasNameFocused);
            wasNameFocused = focused;
        }

        private void SetNameFocused(bool _value)
        {
            if (!ClientWorldScope.IsAlive(clientWorld) || playerStateQuery.IsEmpty) return;

            RefRW<PlayerStateComponent> state = playerStateQuery.GetSingletonRW<PlayerStateComponent>();
            if (state.ValueRO.NameFocused != _value) state.ValueRW.NameFocused = _value;
        }

        /// <summary>
        /// 시작/리스폰 버튼에서 보낼 이름. 입력란이 꺼져 있으면 null(서버가 쓰던 이름 유지).
        /// 규칙에 어긋나면 경고창을 띄우고 false.
        /// </summary>
        protected bool TryTakePlayerName(out string _name)
        {
            _name = null;
            if (!playerName_input.gameObject.activeSelf) return true;

            PlayerNameError error = PlayerNameComponent.Validate(playerName_input.text, out string trimmed);

            if (error != PlayerNameError.None)
            {
                PlayerNameWarning.Show(error);
                return false;
            }

            _name = trimmed;
            playerName_input.SetTextWithoutNotify(trimmed);
            PlayerNameResolver.Remember(trimmed);
            return true;
        }

        public void AddListener(UnityAction _onQuit)
        {
            _quit.AddListener(_onQuit);
        }

        private void OnQuitBtnPress()
        {
            if (!IsActive()) return;
            _quit?.Invoke();
        }

        private void OnCreditBtnPress()
        {
            if (!IsActive()) return;
            OpenPopup(CommonCredit.Open).Forget();
        }

        private void OnSettingsBtnPress()
        {
            if (!IsActive()) return;
            OpenPopup(CommonOption.Open).Forget();
        }

        private async Awaitable OpenPopup(Func<Awaitable> _open)
        {
            // 로드 중 중복 입력 방지. 열린 뒤엔 팝업의 BlockerUI가 입력 차단.
            Deactivate();
            await _open();
            Activate();
        }
    }
}
