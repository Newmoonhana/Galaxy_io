using Unity.NetCode;

namespace JWServer.Runtime.Utils
{
    public static class Definitions
    {
        public static bool IsServerProcess => ClientServerBootstrap.RequestedPlayType == ClientServerBootstrap.PlayType.Server;
    }
}
