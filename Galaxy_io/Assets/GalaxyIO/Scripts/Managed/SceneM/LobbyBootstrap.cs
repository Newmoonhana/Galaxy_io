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
            AddressableAssetsData<GameObject> lobbyUIData = new AddressableAssetsData<GameObject>();
            await lobbyUIData.Load(lobbyUIKey, true);
            lobbyUI = Instantiate(lobbyUIData.Data).GetComponent<LobbyUI>();
            lobbyUIData.UnLoad();
        }
    }
}
