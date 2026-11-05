using GalaxyIO.DOTS.Client.Networking;
using GalaxyIO.DOTS.Components.Singleton;
using UnityEngine;
using UnityEngine.UI;

namespace GalaxyIO.Tools.PopupUI.Window
{
    /// <summary>
    /// 자유 모드 리스폰 창. 리스폰 버튼 하나. 웹은 첫 입장도 이 창(이름 입력 + 시작)으로 시작함.
    /// </summary>
    public class WindowRespawn : WindowRespawnBase
    {
        [SerializeField] private Button respawn_btn;

        protected override void OnEnable()
        {
            base.OnEnable();
            respawn_btn.onClick.AddListener(OnRespawnBtnPress);
        }

        protected override void OnDisable()
        {
            base.OnDisable();
            respawn_btn.onClick.RemoveListener(OnRespawnBtnPress);
        }

        // 요청이 접수되면 태그가 지워져 창이 닫힘.
        private void OnRespawnBtnPress()
        {
            if (!IsActive()) return;
            if (!TryTakePlayerName(out string playerName)) return;

            ClientNetworkRequestManager.RequestRespawn(SpaceshipId.None, playerName);
        }
    }
}
