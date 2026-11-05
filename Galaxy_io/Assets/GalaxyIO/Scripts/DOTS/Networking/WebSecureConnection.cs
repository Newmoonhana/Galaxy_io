using System;
using UnityEngine;

namespace GalaxyIO.DOTS.Networking
{
    /// <summary>
    /// 웹 클라이언트 페이지가 https 로 열렸을 때의 접속 정보.
    /// https 페이지는 ws:// 접속을 막으므로, 서버 앞의 TLS 프록시(wss)로 접속함.
    /// http 페이지와 앱 클라이언트는 원래대로 7979 에 직접 접속함.
    /// </summary>
    public static class WebSecureConnection
    {
        // TLS 프록시가 받는 포트. 프록시가 게임 서버 WebSocket(7979)으로 넘김.
        public const ushort Port = 7980;

        /// <summary>
        /// 페이지가 https 면 페이지 호스트 이름을 돌려줌.
        /// </summary>
        public static bool TryGetHost(out string host)
        {
            host = null;

#if UNITY_WEBGL && !UNITY_EDITOR
            if (Uri.TryCreate(Application.absoluteURL, UriKind.Absolute, out Uri uri)
                && string.Equals(uri.Scheme, "https", StringComparison.OrdinalIgnoreCase)
                && !string.IsNullOrEmpty(uri.Host))
            {
                host = uri.Host;
                return true;
            }
#endif

            return false;
        }

        /// <summary>
        /// 웹 클라이언트가 접속할 포트. https 페이지면 TLS 프록시 포트.
        /// </summary>
        public static ushort ClientPort => TryGetHost(out _) ? Port : GalaxyIO.DOTS.Bootstrap.NetcodeBootstrap.Port;
    }
}
