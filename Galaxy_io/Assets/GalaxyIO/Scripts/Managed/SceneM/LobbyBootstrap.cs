using GalaxyIO.DOTS.Networking;
using GalaxyIO.Tools;
using GalaxyIO.Tools.PopupUI.Popup;
using JWServer.Runtime.Utils;
using Newmoonhana.Runtime.Utils;
using UnityEngine;
using UnityEngine.SceneManagement;

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
            // 데디케이티드 서버는 로비 UI 없이 인게임 씬으로 바로 넘어감.
            if (Definitions.IsServerProcess)
            {
                SceneManager.LoadScene(GameSession.GameScene);
                return;
            }

            LoadLobbyUI().Forget();
        }

        private async Awaitable LoadLobbyUI()
        {
            //로드가 끝나기 전까지 화면이 비므로 로딩 표시를 먼저 띄움.
            //내리는 쪽은 자동 로그인까지 끝낸 LobbyUI.RunAutoLogIn 이 담당함.
            await CommonProgress.ShowAcrossScenes();

            //로비 UI 가 열리자마자 L10n.Get 을 동기로 부름. 테이블을 먼저 올려둬야 웹에서도 동작함.
            await L10n.PreloadTables();

            //전역 캐시가 핸들을 계속 들고 있음. 여기서 해제하면 같은 번들의
            //아틀라스 / 폰트 / 셰이더까지 파괴되어 인스턴스가 빈 껍데기가 됨.
            await AddressableAssetsManager.gameObject_lst.LoadData(lobbyUIKey);
            lobbyUI = Instantiate(AddressableAssetsManager.gameObject_lst.GetData(lobbyUIKey).Data)
                .GetComponent<LobbyUI>();
        }
    }
}
