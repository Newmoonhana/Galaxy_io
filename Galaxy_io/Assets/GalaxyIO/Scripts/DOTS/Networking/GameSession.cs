using GalaxyIO.DOTS.Bootstrap;
using GalaxyIO.DOTS.Client.Networking;
using GalaxyIO.DOTS.Components.Singleton;
using Newmoonhana.Runtime.Tools.Scene;
using Newmoonhana.Runtime.Utils.DEBUG;
using Unity.Entities;
using Unity.NetCode;
using Unity.Networking.Transport;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace GalaxyIO.DOTS.Networking
{
    /// <summary>
    /// 세션(월드 + 인게임 씬) 수명 관리. 역할이 정해진 뒤에 필요한 월드만 만듦.
    /// <p/>
    /// 인게임 씬의 SubScene 은 씬 로드 시점에 존재하는 월드로만 스트리밍됨.
    /// 그래서 월드를 먼저 만들고 씬을 로드함. 접속만 하는 세션에는 서버 월드를 만들지 않으므로
    /// 그 인스턴스에서는 서버 시스템이 아예 생성되지 않음.
    /// </summary>
    public static class GameSession
    {
        private const string LobbyScene = "GalaxyIO_Lobby";
        private const string GameScene = "GalaxyIO_Main";

        // 베이킹 데이터가 월드로 들어오기를 기다리는 최대 프레임 수.
        private const int StreamWaitFrames = 600;

        /// <summary>
        /// 이 인스턴스가 서버를 들고 있는지 여부.
        /// </summary>
        public static bool IsHosting { get; private set; }

        /// <summary>
        /// 세션이 열려 있는지 여부(인게임 씬에 들어와 있음).
        /// </summary>
        public static bool IsActive { get; private set; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStaticData()
        {
            IsHosting = false;
            IsActive = false;
        }

        /// <summary>
        /// 클라이언트로만 접속. 서버 월드를 만들지 않으므로 외부 접속을 받지 않음.
        /// </summary>
        public static async Awaitable JoinAsClient(NetworkEndpoint endpoint, string playerName)
        {
            if (IsActive) return;

            IsActive = true;
            ClientServerBootstrap.CreateClientWorld("ClientWorld");

            await LoadGameScene();
            Connect(endpoint, playerName);
        }

        /// <summary>
        /// 서버를 열고 자기 서버에 접속. 서버 월드가 씬보다 먼저 생겨야 스폰이 동작함.
        /// </summary>
        /// <returns>서버를 열었는지 여부. 실패 시 세션을 시작하지 않음.</returns>
        public static async Awaitable<bool> HostAndPlay(string playerName)
        {
            if (IsActive) return false;

            World server = ClientServerBootstrap.CreateServerWorld("ServerWorld");

            if (!Listen(server))
            {
                server.Dispose();
                return false;
            }

            IsHosting = true;
            IsActive = true;
            ClientServerBootstrap.CreateClientWorld("ClientWorld");

            await LoadGameScene();
            Connect(NetworkEndpoint.Parse(NetcodeBootstrap.LoopbackAddress, NetcodeBootstrap.Port), playerName);
            return true;
        }

        /// <summary>
        /// 세션 종료. 월드를 모두 버리고 로비로 돌아감.
        /// 호스트가 나가면 서버도 함께 닫히므로 접속자들은 끊김.
        /// </summary>
        public static async Awaitable Leave()
        {
            if (!IsActive) return;

            IsActive = false;
            IsHosting = false;

            DisposeWorlds();

            // 로비 씬은 스택 아래에 그대로 있음. 떠날 때 창·입력값 그대로 되살아남.
            await MainSceneStack.Pop();

            // 인게임 씬을 직접 Play 한 디버그 진입은 아래가 로비가 아님. 그때만 새로 로드함.
            if (SceneManager.GetActiveScene().name == LobbyScene) return;

            MainSceneStack.Clear();
            await SceneManager.LoadSceneAsync(LobbyScene);
        }

        /// <summary>
        /// 인게임 씬을 로드하고 베이킹 데이터가 월드에 들어올 때까지 기다림.
        /// </summary>
        private static async Awaitable LoadGameScene()
        {
            // 로비 씬을 남긴 채 덧씌움. 접속에 실패해도 로비를 다시 만들지 않음.
            await MainSceneStack.Push(GameScene);

            World client = ClientServerBootstrap.ClientWorld;
            if (client == null) return;

            using EntityQuery registryQuery = client.EntityManager.CreateEntityQuery(
                ComponentType.ReadOnly<PrefabRegistryElement>()
            );

            for (int i = 0; i < StreamWaitFrames && registryQuery.IsEmpty; i++)
            {
                await Awaitable.NextFrameAsync();
            }

            if (registryQuery.IsEmpty)
            {
                DebugLog.LogError("인게임 씬의 베이킹 데이터가 월드로 들어오지 않음.");
            }
        }

        // 스폰 요청. 실제 접속과 RPC 전송은 `ClientConnectionSystem`이 처리함.
        private static void Connect(NetworkEndpoint endpoint, string playerName)
        {
            ClientNetworkRequestManager.RequestPlayerSpawn(endpoint, playerName);
        }

        // 모든 드라이버가 수신 대기를 시작하게 함.
        private static bool Listen(World server)
        {
            if (server == null) return false;

            using EntityQuery driverQuery = server.EntityManager.CreateEntityQuery(
                ComponentType.ReadWrite<NetworkStreamDriver>()
            );

            return driverQuery.GetSingletonRW<NetworkStreamDriver>().ValueRW
                .Listen(NetworkEndpoint.AnyIpv4.WithPort(NetcodeBootstrap.Port));
        }

        // 이 세션이 만든 월드를 모두 버림. 서버 월드가 사라지면 열어둔 포트도 닫힘.
        private static void DisposeWorlds()
        {
            for (int i = ClientServerBootstrap.ClientWorlds.Count - 1; i >= 0; i--)
            {
                ClientServerBootstrap.ClientWorlds[i].Dispose();
            }

            for (int i = ClientServerBootstrap.ServerWorlds.Count - 1; i >= 0; i--)
            {
                ClientServerBootstrap.ServerWorlds[i].Dispose();
            }
        }
    }
}
