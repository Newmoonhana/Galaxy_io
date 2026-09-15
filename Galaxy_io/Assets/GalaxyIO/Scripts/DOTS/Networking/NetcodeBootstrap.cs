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

        // 루프백 버튼이 입력창에 채워 넣을 주소.
        public const string LoopbackAddress = "127.0.0.1";

        public override bool Initialize(string defaultWorldName)
        {
            // 서버 인바운드 규칙: Any IPv4
            DefaultListenAddress = NetworkEndpoint.AnyIpv4;

            // `NetworkEndpoint.AnyIpv4`를 할당하면 유니티는 "최초 실행 시 서버 자동 접속"을
            // 수행하지 않음. 접속 시점은 게임 시작 버튼이 제어함.
            DefaultConnectAddress = NetworkEndpoint.AnyIpv4;

            // 서버가 Listen 할 포트. 0 이면 Listen 하지 않음.
            // 클라이언트 전용 프로세스는 0. 에디터에서는 이 값이 있으면 PlayMode Tools 주소로 자동 접속함.
            AutoConnectPort = RequestedPlayType == PlayType.Client ? (ushort)0 : Port;

            // 접속 실패를 빠르게 알리도록 타임아웃을 줄인 드라이버 사용.
            NetworkStreamReceiveSystem.DriverConstructor = new NetcodeDriverConstructor();

            return base.Initialize(defaultWorldName);
        }
    }
}
