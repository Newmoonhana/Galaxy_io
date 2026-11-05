using System;
using System.Net;
using System.Net.Sockets;
using GalaxyIO.Camera;
using GalaxyIO.DOTS.Bootstrap;
using GalaxyIO.DOTS.Networking;
using Unity.Networking.Transport;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace GalaxyIO.Spectator
{
    /// <summary>
    /// 관찰자 모드. 실행 인자 -spectator 로 켬. 로비 없이 바로 관찰자로 접속하고 카메라를 관찰자 카메라로 바꿈.
    /// 인자가 없으면 아무것도 하지 않으므로 평소 클라이언트와 똑같이 동작함.
    /// <para/>
    /// 인자: -spectator, -spectate-address 주소(기본 127.0.0.1, 도메인 가능), -spectate-name 이름(기본 @spectator, 서버 설정과 같아야 함)
    /// </summary>
    public static class SpectatorMode
    {
        public const string DefaultName = "@spectator";

        public static bool Enabled { get; private set; }
        public static string Address { get; private set; } = NetcodeBootstrap.LoopbackAddress;
        public static string Name { get; private set; } = DefaultName;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStaticData()
        {
            Enabled = false;
            Address = NetcodeBootstrap.LoopbackAddress;
            Name = DefaultName;
            SceneManager.sceneLoaded -= OnSceneLoaded;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Initialize()
        {
            if (Application.isBatchMode)
            {
                return;
            }

            string[] args = Environment.GetCommandLineArgs();

            for (int i = 0; i < args.Length; i++)
            {
                string arg = args[i];

                if (string.Equals(arg, "-spectator", StringComparison.OrdinalIgnoreCase))
                {
                    Enabled = true;
                }
                else if (string.Equals(arg, "-spectate-address", StringComparison.OrdinalIgnoreCase) && i + 1 < args.Length)
                {
                    Address = args[++i];
                }
                else if (string.Equals(arg, "-spectate-name", StringComparison.OrdinalIgnoreCase) && i + 1 < args.Length)
                {
                    Name = args[++i];
                }
            }

            if (!Enabled)
            {
                return;
            }

            Debug.Log($"[Spectator] 관찰자 모드: {Address}:{NetcodeBootstrap.Port}");
            SceneManager.sceneLoaded += OnSceneLoaded;
        }

        /// <summary>
        /// 접속할 주소. 도메인이면 IPv4 로 풂.
        /// </summary>
        public static bool TryGetEndpoint(out NetworkEndpoint endpoint)
        {
            if (NetworkEndpoint.TryParse(Address, NetcodeBootstrap.Port, out endpoint, NetworkFamily.Ipv4))
            {
                return true;
            }

            try
            {
                foreach (IPAddress ip in Dns.GetHostAddresses(Address))
                {
                    if (ip.AddressFamily == AddressFamily.InterNetwork
                        && NetworkEndpoint.TryParse(ip.ToString(), NetcodeBootstrap.Port, out endpoint, NetworkFamily.Ipv4))
                    {
                        return true;
                    }
                }
            }
            catch (Exception e)
            {
                Debug.LogError($"[Spectator] 주소를 찾지 못함: {Address} ({e.Message})");
            }

            endpoint = default;
            return false;
        }

        // 인게임 씬의 플레이어 카메라를 관찰자 카메라로 바꿈.
        private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            if (scene.name != GameSession.GameScene)
            {
                return;
            }

            foreach (GameObject root in scene.GetRootGameObjects())
            {
                foreach (FollowPlayerEntity follow in root.GetComponentsInChildren<FollowPlayerEntity>(true))
                {
                    follow.enabled = false;

                    if (follow.GetComponent<SpectatorCamera>() == null)
                    {
                        follow.gameObject.AddComponent<SpectatorCamera>();
                    }
                }
            }
        }
    }
}
