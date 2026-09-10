using System;
using Newmoonhana.Runtime.Utils.DEBUG;
using UnityEngine;

namespace GalaxyIO.JWServer.Runtime.DTO
{
    /// <summary>
    /// 인증 정보(액세스 토큰, 리프레시 토큰 보유)<p/>
    /// (큰 DTO가 아니라서 구조체로 만듬. 자주 사용하지는 않겠지만 가벼우면 좋으니까)
    /// </summary>
    [Serializable]
    public readonly struct GameUserTokenDTO
    {
        public readonly string accessToken;
        public readonly string refreshToken;

        public GameUserTokenDTO(string _accessToken, string _refreshToken)
        {
            accessToken = _accessToken;
            refreshToken = _refreshToken;
        }

        public GameUserTokenDTO(GameUserResponseDTO other)
        {
            accessToken = other.accessToken;
            refreshToken = other.refreshToken;
        }

        public override int GetHashCode() => System.HashCode.Combine(accessToken, refreshToken);
        public override bool Equals(object obj)
        {
            DebugLog.LogError("사용 금지 함수 호출!");
            return false;
        }
        public bool Equals(GameUserTokenDTO other) => accessToken == other.accessToken && refreshToken == other.refreshToken;
        public static bool operator ==(GameUserTokenDTO a, GameUserTokenDTO b) => a.Equals(b);
        public static bool operator !=(GameUserTokenDTO a, GameUserTokenDTO b) => !(a.Equals(b));
        public bool IsVaild() => !string.IsNullOrEmpty(accessToken) || !string.IsNullOrEmpty(refreshToken);
    }
}
