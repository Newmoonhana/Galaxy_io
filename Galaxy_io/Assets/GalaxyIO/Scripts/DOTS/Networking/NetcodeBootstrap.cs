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

            // 자동 Listen과 자동 접속을 모두 끔. 서버는 `GameSession`이 열 때만 Listen 함.
            AutoConnectPort = 0;

            // 접속 실패를 빠르게 알리도록 타임아웃을 줄인 드라이버 사용.
            NetworkStreamReceiveSystem.DriverConstructor = new NetcodeDriverConstructor();

            // 역할(접속/호스팅)이 정해지기 전에는 클라이언트/서버 월드를 만들지 않음.
            // 월드는 `GameSession`이 만들고, 그 뒤에 인게임 씬을 로드함.
            // 인게임 씬의 SubScene 은 로드 시점에 존재하는 월드로만 스트리밍되므로 순서가 중요함.
            CreateLocalWorld(defaultWorldName);
            return true;
        }
    }
}
