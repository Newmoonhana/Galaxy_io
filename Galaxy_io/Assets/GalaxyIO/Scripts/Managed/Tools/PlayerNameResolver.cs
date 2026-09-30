using GalaxyIO.DOTS.Components.Actor;
using JWServer.Runtime.Login;
using UnityEngine;

namespace GalaxyIO.Tools
{
    /// <summary>
    /// 접속에 사용할 플레이어 이름 결정. 사설 서버 비로그인 접속만 직접 입력을 허용함.
    /// </summary>
    public static class PlayerNameResolver
    {
        // 직접 입력한 이름. 세션 동안 유지함.
        private static string sessionName = string.Empty;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStaticData()
        {
            sessionName = string.Empty;
        }

        /// <summary>
        /// 이름을 직접 입력할 수 있는지 여부.
        /// </summary>
        public static bool IsEditable(bool isPrivateServer) => isPrivateServer && !LoginManager.IsOnline;

        /// <summary>
        /// 입력창에 채울 이름. 편집 가능하면 세션에 남은 이름, 없으면 고정 이름을 제안함.
        /// </summary>
        public static string Suggest(bool isPrivateServer)
            => IsEditable(isPrivateServer) && !string.IsNullOrWhiteSpace(sessionName) ? sessionName : Fixed();

        /// <summary>
        /// 직접 입력한 이름을 세션에 기억함.
        /// </summary>
        public static void Remember(string name)
        {
            sessionName = name;
        }

        // 로그인 유저는 계정 닉네임, 비로그인 유저는 게스트 이름.
        private static string Fixed()
        {
            string nickName = LoginManager.NickName;

            if (!LoginManager.IsOnline || string.IsNullOrEmpty(nickName))
                return $"Pilot#{Random.Range(0, 10000):D4}";

            return nickName.Length > PlayerNameComponent.MaxLength
                ? nickName.Substring(0, PlayerNameComponent.MaxLength)
                : nickName;
        }
    }
}
