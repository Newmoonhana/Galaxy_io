

namespace JWServer.Runtime.Utils
{
    public static class ResponseMsgUtil
    {
        /// <summary>
        /// 서버 응답이 긍정적 응답(e.g. 로그인 성공, 토큰 재발급 성공 등)인지를 반환
        /// </summary>
        public static bool IsOk(string msg)
        {
            return msg is RefreshTokenMsg.OK or GameStartMsg.SUCCESS;
        }

        /// <summary>
        /// 사용자 정보 불일치에 의한 오류(아이디, 비밀번호 오입력)인지를 반환
        /// </summary>
        public static bool IsInvalidCredential(string msg)
        {
            return msg is RefreshTokenMsg.INVALID_CREDENTIALS;
        }

        /// <summary>
        /// 서버 연결에 실패했는지 여부 반환(e.g. 서버가 다운됐거나, 디바이스가 오프라인)
        /// </summary>
        public static bool IsServerUnreachable(string msg)
        {
            return msg is CommonMsg.CONNECTION_ERROR;
        }

        /// <summary>
        /// 서버 연결에 실패했는지 여부 반환(e.g. 서버가 다운됐거나, 디바이스가 오프라인)
        /// </summary>
        public static bool IsAccessTokenExpired(string msg)
        {
            return msg is GameStartMsg.INVALID_ACCESS_TOKEN or GameStartMsg.ACCESS_TOKEN_EXPIRED;
        }

        /// <summary>
        ///
        /// </summary>
        public static bool IsLogin(string msg)
        {
            return IsOk(msg) || msg is CommonMsg.ALREADY_ONLINE;
        }
    }
}
