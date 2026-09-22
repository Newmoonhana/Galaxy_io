using System;

namespace JWServer.Runtime.DTO
{
    /// <summary>
    /// JW 게임 서버 `/api/account/memberiddupcheck` 요청 DTO<p/>
    /// 서버가 정의 외 키를 거절함 -> 필드 추가 금지
    /// </summary>
    [Serializable]
    public struct MemberIdDupCheckRequestDTO
    {
        /// <summary>
        /// JW 서버에서 발급한 게임 코드
        /// </summary>
        public string gameCode;

        /// <summary>
        /// 확인할 멤버 ID
        /// </summary>
        public string memberId;

        public MemberIdDupCheckRequestDTO(string _gameCode, string _memberId)
        {
            gameCode = _gameCode;
            memberId = _memberId;
        }
    }
}
