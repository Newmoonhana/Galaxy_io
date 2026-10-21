using GalaxyIO.Tools.PopupUI.Popup;
using Newmoonhana.Runtime.Utils;
using UnityEngine;

namespace GalaxyIO.Main
{
    /// <summary>
    /// 로비 씬 진입점. Lobby UI 를 띄우는 일만 함.
    /// 세션(월드·인게임 씬)은 `GameSession`이 관리함.
    /// </summary>
    public sealed class LobbyBootstrap : MonoBehaviour
    {
        private const string lobbyUIKey = "Lobby_UI";

        private LobbyUI lobbyUI;

        private void Start()
        {
            _ = LoadLobbyUI();
        }

        private async Awaitable LoadLobbyUI()
        {
            //전역 캐시가 핸들을 계속 들고 있음. 여기서 해제하면 같은 번들의
            //아틀라스 / 폰트 / 셰이더까지 파괴되어 인스턴스가 빈 껍데기가 됨.
            await AddressableAssetsManager.gameObject_lst.LoadData(lobbyUIKey);
            lobbyUI = Instantiate(AddressableAssetsManager.gameObject_lst.GetData(lobbyUIKey).Data)
                .GetComponent<LobbyUI>();
        }
    }
}
