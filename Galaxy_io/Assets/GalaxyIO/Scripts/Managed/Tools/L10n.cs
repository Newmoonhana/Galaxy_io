using UnityEngine.Localization.Settings;

namespace GalaxyIO.Tools
{
    /// <summary>
    /// 코드에서 쓰는 로컬라이즈 문자열 조회. 테이블은 GalaxyIO_UI 하나만 사용함.
    /// </summary>
    public static class L10n
    {
        public const string tableName = "GalaxyIO_UI";

        /// <summary>
        /// 표시 시점의 언어로 조회. 로컬 테이블이라 동기 반환.
        /// </summary>
        public static string Get(string _key)
            => LocalizationSettings.StringDatabase.GetLocalizedString(tableName, _key);
    }
}
