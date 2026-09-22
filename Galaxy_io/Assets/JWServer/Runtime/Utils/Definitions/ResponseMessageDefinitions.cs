

namespace JWServer.Runtime.Utils
{
    // 서버 계열 메세지고, string 값으로 오류 타입을 받아오기 때문에 string끼리 비교 방식을 택함 
    public static class RefreshTokenMsg
    {
        public const string OK = "OK";
        public const string MANDATORY_PARAM_MISSING = "MANDATORY_PARAM_MISSING";
        public const string INVALID_PARAMETER = "INVALID_PARAMETER";
        public const string UNSUPPORTED_MEMBER_TYPE = "UNSUPPORTED_MEMBER_TYPE";
        public const string UNSUPPORTED_ACTION_TYPE = "UNSUPPORTED_ACTION_TYPE";
        public const string UNSUPPORTED_SNS_PROVIDER = "UNSUPPORTED_SNS_PROVIDER";
        public const string INVALID_JWT_TOKEN = "INVALID_JWT_TOKEN";
        public const string EXPIRED_JWT_TOKEN = "EXPIRED_JWT_TOKEN";
        public const string INVALID_CREDENTIALS = "INVALID_CREDENTIALS";
        public const string USER_ACCESS_DENIED = "USER_ACCESS_DENIED";
        public const string GAME_CODE_NOT_FOUND = "GAME_CODE_NOT_FOUND";
        public const string GAME_ACCOUNT_NOT_FOUND = "GAME_ACCOUNT_NOT_FOUND";
        public const string ACCOUNT_ALREADY_EXISTS = "ACCOUNT_ALREADY_EXISTS";
        public const string NICKNAME_ALREADY_EXISTS = "NICKNAME_ALREADY_EXISTS";
        public const string EXCEPTION_OCCURRED = "EXCEPTION_OCCURRED";
        public const string SERVICE_UNDER_MAINTENANCE = "SERVICE_UNDER_MAINTENANCE";
        public const string SERVICE_BLOCKED = "SERVICE_BLOCKED";
    }

    public static class GameStartMsg
    {
        public const string SUCCESS = "SUCCESS";
        public const string MANDATORY_PARAM_MISSING = "MANDATORY_PARAM_MISSING";
        public const string INVALID_ACCESS_TOKEN = "INVALID_ACCESS_TOKEN";
        public const string ACCESS_TOKEN_EXPIRED = "ACCESS_TOKEN_EXPIRED";
        public const string GAME_NOT_FOUND = "GAME_NOT_FOUND";
        public const string GAME_NOT_AVAILABLE = "GAME_NOT_AVAILABLE";
        public const string GAME_ACCOUNT_NOT_FOUND = "GAME_ACCOUNT_NOT_FOUND";
        public const string GAME_ACCOUNT_NOT_ACTIVE = "GAME_ACCOUNT_NOT_ACTIVE";
        public const string USER_NOT_ACTIVE = "USER_NOT_ACTIVE";
        public const string EXCEPTION_OCCURRED = "EXCEPTION_OCCURRED";
    }

    public static class CommonMsg
    {
        public const string ALREADY_ONLINE = "ALREADY_ONLINE";
        public const string NO_AUTH_INFO_IN_LOCAL = "NO_AUTH_INFO_IN_LOCAL";
        public const string CONNECTION_ERROR = "CONNECTION_ERROR"; // Server downed, or offline device.
    }
}