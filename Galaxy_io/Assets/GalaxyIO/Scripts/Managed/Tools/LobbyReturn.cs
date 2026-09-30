using UnityEngine;

namespace GalaxyIO.Tools
{
    /// <summary>
    /// 인게임에서 로비로 돌아올 때 로비에 전할 안내. 로비가 다시 켜질 때 한 번만 소비됨.
    /// 안내가 남아 있으면 떠날 때 창을 그대로 두고, 없으면 로비 메인으로 되돌림.
    /// </summary>
    public static class LobbyReturn
    {
        private static string noticeTitle;
        private static string noticeBody;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStaticData()
        {
            noticeTitle = null;
            noticeBody = null;
        }

        /// <summary>
        /// 로비 복귀 직후 띄울 안내를 남김.
        /// </summary>
        public static void SetNotice(string _title, string _body)
        {
            noticeTitle = _title;
            noticeBody = _body;
        }

        /// <summary>
        /// 남은 안내를 꺼내고 비움. 없으면 false.
        /// </summary>
        public static bool TakeNotice(out string _title, out string _body)
        {
            _title = noticeTitle;
            _body = noticeBody;

            noticeTitle = null;
            noticeBody = null;

            return _body != null;
        }
    }
}
