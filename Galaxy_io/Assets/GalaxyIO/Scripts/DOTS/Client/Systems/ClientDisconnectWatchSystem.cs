using GalaxyIO.DOTS.Client.Components.Message;
using GalaxyIO.DOTS.Networking;
using Unity.Entities;
using Unity.NetCode;

namespace GalaxyIO.DOTS.Client.Systems
{
    /// <summary>
    /// 끊김 이벤트를 받아 사유를 남기는 시스템. 안내창이 읽어 표시함.
    /// </summary>
    [WorldSystemFilter(WorldSystemFilterFlags.ClientSimulation)]
    public partial struct ClientDisconnectWatchSystem : ISystem
    {
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<NetworkStreamDriver>();
        }

        public void OnUpdate(ref SystemState state)
        {
            foreach (NetCodeConnectionEvent connectionEvent in SystemAPI.GetSingleton<NetworkStreamDriver>().ConnectionEventsForTick)
            {
                if (connectionEvent.State != ConnectionState.State.Disconnected)
                {
                    continue;
                }

                Entity entity = state.EntityManager.CreateEntity();
                state.EntityManager.AddComponentData(entity, new ClientDisconnectedMessage
                {
                    Reason = Reason(connectionEvent.DisconnectReason)
                });
            }
        }

        private static string Reason(NetworkStreamDisconnectReason reason)
        {
            switch (reason)
            {
                case NetworkStreamDisconnectReason.ConnectionClose: return NetworkMsg.CONNECTION_CLOSED;
                case NetworkStreamDisconnectReason.Timeout: return NetworkMsg.TIMEOUT;
                case NetworkStreamDisconnectReason.MaxConnectionAttempts: return NetworkMsg.MAX_CONNECTION_ATTEMPTS;
                case NetworkStreamDisconnectReason.ClosedByRemote: return NetworkMsg.CLOSED_BY_REMOTE;
                case NetworkStreamDisconnectReason.BadProtocolVersion: return NetworkMsg.BAD_PROTOCOL_VERSION;
                case NetworkStreamDisconnectReason.InvalidRpc: return NetworkMsg.INVALID_RPC;
                case NetworkStreamDisconnectReason.AuthenticationFailure: return NetworkMsg.AUTHENTICATION_FAILURE;
                case NetworkStreamDisconnectReason.ProtocolError: return NetworkMsg.PROTOCOL_ERROR;
                default: return NetworkMsg.UNKNOWN;
            }
        }
    }
}
