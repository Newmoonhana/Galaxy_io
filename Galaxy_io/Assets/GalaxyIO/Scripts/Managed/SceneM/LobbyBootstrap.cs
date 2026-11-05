using GalaxyIO.DOTS.Networking;
using GalaxyIO.Tools;
using GalaxyIO.Tools.PopupUI.Popup;
using JWServer.Runtime.Utils;
using Newmoonhana.Runtime.Utils;
using UnityEngine;
using UnityEngine.SceneManagement;
#if UNITY_WEBGL && !UNITY_EDITOR
using System;
using System.Reflection;
using GalaxyIO.DOTS.Bootstrap;
using Unity.Collections;
using Unity.Networking.Transport;
#endif

namespace GalaxyIO.Main
{
    /// <summary>
    /// 로비 씬 진입점. Lobby UI 를 띄우는 일만 함. 웹은 로비 UI 없이 바로 서버에 접속함.
    /// 세션(월드·인게임 씬)은 `GameSession`이 관리함.
    /// </summary>
    public sealed class LobbyBootstrap : MonoBehaviour
    {
        private const string lobbyUIKey = "Lobby_UI";

        private LobbyUI lobbyUI;

#if UNITY_WEBGL && !UNITY_EDITOR
        // 최초 진입이 끝났는지. 이후의 OnEnable 은 인게임에서 돌아온 것.
        private bool started;

        // 인게임에서 돌아오면 씬 루트가 다시 켜짐. 남은 안내가 있으면 띄우고, 없으면 바로 다시 접속함.
        private void OnEnable()
        {
            if (!started) return;

            if (LobbyReturn.TakeNotice(out string title, out string body))
            {
                ShowRetryNotice(title, body).Forget();
                return;
            }

            JoinWebServer().Forget();
        }
#endif

        private void Start()
        {
            // 데디케이티드 서버는 로비 UI 없이 인게임 씬으로 바로 넘어감.
            if (Definitions.IsServerProcess)
            {
                SceneManager.LoadScene(GameSession.GameScene);

                string modeScene = GameSession.SceneOf(GameSession.Mode);
                if (modeScene != null) SceneManager.LoadScene(modeScene, LoadSceneMode.Additive);
                return;
            }

            // 관찰자 모드(-spectator)는 로비 UI 없이 바로 관찰자로 접속함.
            if (Spectator.SpectatorMode.Enabled)
            {
                JoinAsSpectator().Forget();
                return;
            }

            LoadLobbyUI().Forget();
        }

        /// <summary>
        /// 관찰자로 접속. 실패하면 몇 초 뒤 다시 시도함.
        /// </summary>
        private async Awaitable JoinAsSpectator()
        {
            await CommonProgress.ShowAcrossScenes();
            await L10n.PreloadTables();

            while (this != null)
            {
                if (Spectator.SpectatorMode.TryGetEndpoint(out Unity.Networking.Transport.NetworkEndpoint endpoint))
                {
                    string failure = await GameSession.JoinAsClient(endpoint, Spectator.SpectatorMode.Name);

                    if (failure == null)
                    {
                        return;
                    }

                    Debug.LogWarning($"[Spectator] 접속 실패: {failure}. 3초 뒤 다시 시도함.");
                }

                await Awaitable.WaitForSecondsAsync(3.0F);
            }
        }

        private async Awaitable LoadLobbyUI()
        {
            //로드가 끝나기 전까지 화면이 비므로 로딩 표시를 먼저 띄움.
            //내리는 쪽은 자동 로그인까지 끝낸 LobbyUI.RunAutoLogIn 이 담당함.
            await CommonProgress.ShowAcrossScenes();

            //로비 UI 가 열리자마자 L10n.Get 을 동기로 부름. 테이블을 먼저 올려둬야 웹에서도 동작함.
            await L10n.PreloadTables();

#if UNITY_WEBGL && !UNITY_EDITOR
            started = true;
            await JoinWebServer();
#else
            //전역 캐시가 핸들을 계속 들고 있음. 여기서 해제하면 같은 번들의
            //아틀라스 / 폰트 / 셰이더까지 파괴되어 인스턴스가 빈 껍데기가 됨.
            await AddressableAssetsManager.gameObject_lst.LoadData(lobbyUIKey);
            lobbyUI = Instantiate(AddressableAssetsManager.gameObject_lst.GetData(lobbyUIKey).Data)
                .GetComponent<LobbyUI>();
#endif
        }

#if UNITY_WEBGL && !UNITY_EDITOR
        /// <summary>
        /// 웹 페이지를 연 호스트의 데디케이티드 서버로 접속함. 이름은 리스폰 창에서 입력함.
        /// </summary>
        private async Awaitable JoinWebServer()
        {
            // 씬 로드·스트리밍·접속 동안 빈 화면이 보이지 않게 가림. 인게임에서 치움.
            await CommonProgress.ShowAcrossScenes();

            string host = Uri.TryCreate(Application.absoluteURL, UriKind.Absolute, out Uri uri) ? uri.Host : string.Empty;

            string failure = GameSession.Unreachable;

            if (!string.IsNullOrEmpty(host))
            {
                // 도메인은 브라우저가 해석함. 웹 Transport 의 도메인 주소 생성자가 internal 이라 리플렉션으로 만듦.
                // https 페이지면 TLS 프록시 포트(wss), 아니면 게임 서버 포트(ws).
                ushort port = WebSecureConnection.ClientPort;

                if (!NetworkEndpoint.TryParse(host, port, out NetworkEndpoint endpoint, NetworkFamily.Ipv4))
                {
                    ConstructorInfo hostnameEndpoint = typeof(NetworkEndpoint).GetConstructor(
                        BindingFlags.Instance | BindingFlags.NonPublic, null, new[] { typeof(FixedString512Bytes) }, null);

                    endpoint = (NetworkEndpoint)hostnameEndpoint.Invoke(new object[] { new FixedString512Bytes($"{host}:{port}") });
                }

                // 이름은 리스폰 창에서 입력. 입장은 기억한 이름(없으면 임시 이름)으로 하고 스폰은 미룸.
                // 웹 페이지(index.html)에서 닉네임을 입력했으면 그 이름을 리스폰 창 입력란에 미리 채움.
                // 임시 이름은 한 번만 뽑아 기억함. 리스폰 창 입력란·재접속이 같은 이름을 씀.
                string playerName = WebPlayerName.FromPage() ?? PlayerNameResolver.Suggest(true);
                PlayerNameResolver.Remember(playerName);

                failure = await GameSession.JoinAsClient(endpoint, playerName, hold: true);
            }

            if (failure == null) return;

            await ShowRetryNotice(L10n.Get("GameStart/FailedTitle"), L10n.Get(failure));
        }

        /// <summary>
        /// 안내창을 띄우고, 닫으면 다시 접속함.
        /// </summary>
        private async Awaitable ShowRetryNotice(string _title, string _body)
        {
            CommonProgress.HideAcrossScenes();

            CommonMessageBox msgBox = await CommonMessageBox.Open();
            if (msgBox == null) return;

            msgBox.SetWinText(_title, _body);
            msgBox.Refresh();
            msgBox.AddListener(() => JoinWebServer().Forget());
        }
#endif
    }
}
