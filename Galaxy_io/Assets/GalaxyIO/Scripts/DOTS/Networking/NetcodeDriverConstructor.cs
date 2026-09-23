using Unity.Entities;
using Unity.NetCode;
using Unity.Networking.Transport;

namespace GalaxyIO.DOTS.Bootstrap
{
    /// <summary>
    /// 접속/끊김 타임아웃을 줄인 NetworkDriver 생성기.
    /// </summary>
    public struct NetcodeDriverConstructor : INetworkStreamDriverConstructor
    {
        // 접속 시도 간격(ms).
        private const int ConnectTimeoutMS = 1000;

        // 최대 접속 시도 횟수. 넘기면 접속 실패로 처리됨.
        private const int MaxConnectAttempts = 5;

        // 상대에게서 이 시간(ms) 동안 패킷이 없으면 접속을 끊음. 서버/클라이언트 공용.
        private const int DisconnectTimeoutMS = 5000;

        public void CreateClientDriver(World world, ref NetworkDriverStore driverStore, NetDebug netDebug)
        {
            NetworkSettings settings = DefaultDriverBuilder.GetNetworkClientSettings();

            if (settings.TryGet(out NetworkConfigParameter config))
            {
                config.connectTimeoutMS = ConnectTimeoutMS;
                config.maxConnectAttempts = MaxConnectAttempts;
                config.disconnectTimeoutMS = DisconnectTimeoutMS;
                settings.AddRawParameterStruct(ref config);
            }

            DefaultDriverBuilder.RegisterClientDriver(world, ref driverStore, netDebug, settings);
        }

        public void CreateServerDriver(World world, ref NetworkDriverStore driverStore, NetDebug netDebug)
        {
            NetworkSettings settings = DefaultDriverBuilder.GetNetworkServerSettings();

            if (settings.TryGet(out NetworkConfigParameter config))
            {
                config.disconnectTimeoutMS = DisconnectTimeoutMS;
                settings.AddRawParameterStruct(ref config);
            }

            DefaultDriverBuilder.RegisterServerDriver(world, ref driverStore, netDebug, settings);
        }
    }
}
