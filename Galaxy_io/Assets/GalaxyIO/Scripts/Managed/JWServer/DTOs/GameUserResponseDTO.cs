using System;
using Newmoonhana.Runtime.Utils.DEBUG;
using UnityEngine;

namespace GalaxyIO.JWServer.Runtime.DTO
{
    /// <summary>
    /// JW 게임 서버에서 응답을 받을 때의 DTO (한번에 받아오기 때문에 멤버 변수가 많음)<p/>
    /// JsonUtility로 역직렬화되므로 필드명이 곧 JSON 키가 됨 -> 반드시 서버 스펙과 동일하게 유지할 것
    /// </summary>
    [Serializable]
    public readonly struct GameUserResponseDTO
    {
        /// <summary>
        /// 응답 메세지
        /// </summary>
        public readonly string responseMessage;

        /// <summary>
        /// 엑세스 토큰
        /// </summary>
        public readonly string accessToken;
        /// <summary>
        /// 리프레쉬 토큰
        /// </summary>
        public readonly string refreshToken;
        /// <summary>
        /// 토큰 타입 — BEARER
        /// </summary>
        public readonly string tokenType;
        /// <summary>
        /// Access Token 만료 시간 — 1시간부터 카운트
        /// </summary>
        public readonly long expiresIn;
        /// <summary>
        /// Refresh Token 만료 시간 — 1달부터 카운트
        /// </summary>
        public readonly long refreshExpiresIn;

        /// <summary>
        /// 신규 계정 여부
        /// </summary>
        public readonly bool isNewAccount;

        /// <summary>
        /// 유저 고유 ID
        /// </summary>
        public readonly string userId;

        /// <summary>
        /// 게임 계정 해시코드
        /// </summary>
        public readonly string gameAccountHashcode;

        public GameUserResponseDTO(string _responseMessage = "", string _accessToken = "",
            string _refreshToken = "", string _tokenType = "", long _expiresIn = -1,
            long _refreshExpiresIn = -1, bool _isNewAccount = false, string _userId = "",
            string _gameAccountHashcode = "")
        {
            responseMessage = _responseMessage;
            accessToken = _accessToken;
            refreshToken = _refreshToken;
            tokenType = _tokenType;
            expiresIn = _expiresIn;
            refreshExpiresIn = _refreshExpiresIn;
            isNewAccount = _isNewAccount;
            userId = _userId;
            gameAccountHashcode = _gameAccountHashcode;
        }
        
        public override int GetHashCode() => System.HashCode.Combine(accessToken, refreshToken);
        public override bool Equals(object obj)
        {
            DebugLog.LogError("사용 금지 함수 호출!");
            return false;
        }
        public bool Equals(GameUserResponseDTO other)
        {
            DebugLog.LogError("사용 금지 함수 호출!");
            return false;
        }
        public static bool operator ==(GameUserResponseDTO a, GameUserResponseDTO b) => a.Equals(b);
        public static bool operator !=(GameUserResponseDTO a, GameUserResponseDTO b) => !(a.Equals(b));
        public bool IsVaild()
        {
            DebugLog.LogError("사용 금지 함수 호출!");
            return false;
        }
    }
}
