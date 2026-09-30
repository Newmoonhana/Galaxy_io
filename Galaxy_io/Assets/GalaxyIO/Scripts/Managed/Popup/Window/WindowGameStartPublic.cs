using GalaxyIO.DOTS.Components.Actor;
using GalaxyIO.Tools;
using JWServer.Runtime.Login;
using TMPro;
using UnityEngine;

namespace GalaxyIO.Tools.PopupUI.Window
{
    /// <summary>
    /// 공개 서버 접속 창. 주소는 인증 서버 호스트 고정, 이름은 계정 기준으로 정해짐.
    /// </summary>
    public class WindowGameStartPublic : WindowGameStartBase
    {
        [SerializeField] private TMP_InputField playerName_input;

        // 창을 여는 시점에 정해진 이름. 게스트 이름은 조회할 때마다 달라져서 잡아둠.
        private string playerName;

        protected override string Address => LoginManager.ServerHost;

        protected override void Awake()
        {
            base.Awake();

            // 공개 서버 이름은 계정 기준으로 고정이라 항상 읽기 전용임.
            playerName_input.characterLimit = PlayerNameComponent.MaxLength;
            playerName_input.interactable = false;
        }

        public override void Open()
        {
            base.Open();

            playerName = PlayerNameResolver.Suggest(false);
            playerName_input.SetTextWithoutNotify(playerName);
        }

        // 계정 닉네임/게스트 이름으로 고정이라 항상 유효함.
        protected override string TakePlayerName() => playerName;
    }
}
