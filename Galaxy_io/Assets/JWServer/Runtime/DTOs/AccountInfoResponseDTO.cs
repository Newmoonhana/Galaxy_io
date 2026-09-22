using System;

namespace JWServer.Runtime.DTO
{
    /// <summary>
    /// JW 게임 서버 `/api/game/accountinfo` 응답 DTO<p/>
    /// JsonUtility로 역직렬화되므로 필드명이 곧 JSON 키가 됨 -> 반드시 서버 스펙과 동일하게 유지할 것
    /// </summary>
    [Serializable]
    public struct AccountInfoResponseDTO
    {
        /// <summary>
        /// 응답 메세지
        /// </summary>
        public string responseMessage;

        /// <summary>
        /// 게임 계정 닉네임
        /// </summary>
        public string accountNickName;
    }
}
