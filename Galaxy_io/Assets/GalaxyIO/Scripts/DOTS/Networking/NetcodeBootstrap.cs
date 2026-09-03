using Unity.NetCode;
using Unity.Networking.Transport;
using UnityEngine.Scripting;

namespace GalaxyIO.DOTS.Bootstrap
{
    /// <summary>
    /// 클라이언트/서버 월드가 생성될 때 사용할 기본 엔드포인트를 지정함
    /// </summary>
    [Preserve]
    public sealed class NetcodeBootstrap : ClientServerBootstrap
    {
        // Unity Netcode 포트
        public const ushort Port = 7979;

        /// <summary>
        /// 클라이언트가 접속할 서버 주소.
        /// 자동 접속을 껐기 때문에 DefaultConnectAddress 대신 이 값을 사용한다.
        /// TODO: Dedicated server가 원격 디바이스에서 작동할 시 IP변경
        /// </summary>
        public static NetworkEndpoint ServerEndpoint => NetworkEndpoint.LoopbackIpv4.WithPort(Port);

        public override bool Initialize(string defaultWorldName)
        {
            // 서버 인바운드 규칙: Any IPv4
            DefaultListenAddress = NetworkEndpoint.AnyIpv4;

            // 클라이언트 자동 접속 비활성화.
            //
            // 자동 접속은 ConfigureClientWorldSystem.OnCreate 에서 단 한 번,
            // 첫 씬이 로드되기도 전에 일어난다. 접속 시점을 게임 시작 버튼이 제어하도록
            // AnyIpv4 를 넣어 자동 접속만 끈다.
            // (HasDefaultAddressAndPortSet 이 AnyIpv4 를 "자동 접속 안 함"으로 취급한다.)
            //
            // 서버의 자동 Listen 은 DefaultListenAddress 를 쓰므로 영향받지 않는다.
            DefaultConnectAddress = NetworkEndpoint.AnyIpv4;

            // 서버가 Listen 할 포트. 0 으로 두면 서버도 Listen 하지 않으므로 유지한다.
            AutoConnectPort = Port;

            return base.Initialize(defaultWorldName);
        }
    }
}
