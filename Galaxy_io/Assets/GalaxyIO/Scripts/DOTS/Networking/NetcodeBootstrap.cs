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
        private const ushort Port = 7979;

        public override bool Initialize(string defaultWorldName)
        {
            // 서버 인바운드 규칙: Any IPv4
            DefaultListenAddress = NetworkEndpoint.AnyIpv4;

            // 클라이언트: Loopback주소(127.0.0.1)로 연결
            // TODO: Dedicated server가 원격 디바이스에서 작동할 시 IP변경
            DefaultConnectAddress = NetworkEndpoint.LoopbackIpv4;

            // 자동 연결 포트 설정
            AutoConnectPort = Port;

            return base.Initialize(defaultWorldName);
        }
    }
}