namespace GalaxyIO.DOTS.Networking
{
    // Netcode 접속이 끊긴 사유. 유저에게 그대로 노출됨.
    public static class NetworkMsg
    {
        public const string CONNECTION_CLOSED = "CONNECTION_CLOSED";
        public const string TIMEOUT = "TIMEOUT";
        public const string MAX_CONNECTION_ATTEMPTS = "MAX_CONNECTION_ATTEMPTS";
        public const string CLOSED_BY_REMOTE = "CLOSED_BY_REMOTE";
        public const string BAD_PROTOCOL_VERSION = "BAD_PROTOCOL_VERSION";
        public const string INVALID_RPC = "INVALID_RPC";
        public const string AUTHENTICATION_FAILURE = "AUTHENTICATION_FAILURE";
        public const string PROTOCOL_ERROR = "PROTOCOL_ERROR";
        public const string UNKNOWN = "UNKNOWN";
    }
}
