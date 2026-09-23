using System;

namespace JWServer.Runtime.DTO
{
    /// <summary>
    /// JW 게임 서버 `/api/account/check` 요청 DTO<p/>
    /// 서버가 memberId, memberPw 외의 필드를 400으로 거부함 -> 필드 추가 금지
    /// </summary>
    [Serializable]
    public struct MemberCheckRequestDTO
    {
        /// <summary>
        /// 멤버 ID (Mandatory)
        /// </summary>
        public string memberId;

        /// <summary>
        /// 멤버 비밀번호 (Mandatory)
        /// </summary>
        public string memberPw;

        public MemberCheckRequestDTO(string _memberId, string _memberPw)
        {
            memberId = _memberId;
            memberPw = _memberPw;
        }
    }
}
