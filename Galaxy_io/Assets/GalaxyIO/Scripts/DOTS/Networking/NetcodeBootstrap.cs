using Unity.NetCode;
using Unity.Networking.Transport;
using UnityEngine.Scripting;

namespace GalaxyIO.DOTS.Bootstrap
{
    /// <summary>
    /// 클라이언트/서버 월드가 생성될 때 사용할 기본 엔드포인트를 지정함.
    /// </summary>
    [Preserve]
    public sealed class NetcodeBootstrap : ClientServerBootstrap
    {
        // Unity Netcode 포트
        public const ushort Port = 7979;

        /// <summary>
        /// 클라이언트가 접속할 서버 주소.
        /// TODO: Dedicated server가 원격 디바이스에서 작동할 시 IP변경
        /// </summary>
        public static NetworkEndpoint ServerEndpoint => NetworkEndpoint.LoopbackIpv4.WithPort(Port);

        public override bool Initialize(string defaultWorldName)
        {
            // 서버 인바운드 규칙: Any IPv4
            DefaultListenAddress = NetworkEndpoint.AnyIpv4;

            // `NetworkEndpoint.AnyIpv4`를 할당하면 유니티는 "최초 실행 시 서버 자동 접속"을
            // 수행하지 않음. 접속 시점은 게임 시작 버튼이 제어함.
            DefaultConnectAddress = NetworkEndpoint.AnyIpv4;

            // 서버가 Listen 할 포트. 0 이면 Listen 하지 않음.
            AutoConnectPort = Port;

            return base.Initialize(defaultWorldName);
        }
    }
}
