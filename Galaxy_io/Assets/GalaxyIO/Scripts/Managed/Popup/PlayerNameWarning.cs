using GalaxyIO.DOTS.Components.Actor;
using UnityEngine;

namespace GalaxyIO.Tools.PopupUI.Popup
{
    /// <summary>
    /// 이름 규칙(<see cref="PlayerNameComponent.Validate"/>) 위반 경고창.
    /// 로비 이름 입력, 웹 리스폰 창, 서버 거절 알림이 함께 씀.
    /// </summary>
    public static class PlayerNameWarning
    {
        /// <summary>
        /// 경고창을 띄움. 부모를 주지 않으면 자체 Canvas 로 뜸.
        /// </summary>
        public static void Show(PlayerNameError _error) => ShowAsync(_error, null).Forget();

        /// <inheritdoc cref="Show(PlayerNameError)"/>
        public static void Show(PlayerNameError _error, Transform _parent) => ShowAsync(_error, _parent).Forget();

        private static async Awaitable ShowAsync(PlayerNameError _error, Transform _parent)
        {
            if (_error == PlayerNameError.None) return;

            CommonMessageBox msgBox = await CommonMessageBox.Open(_parent);
            if (msgBox == null) return;

            string body = _error == PlayerNameError.Empty
                ? L10n.Get("PlayerName/Empty")
                : string.Format(L10n.Get("PlayerName/TooLong"), PlayerNameComponent.MaxLength);

            msgBox.SetWinText(L10n.Get("PlayerName/WarningTitle"), body);
            msgBox.Refresh();
        }
    }
}
