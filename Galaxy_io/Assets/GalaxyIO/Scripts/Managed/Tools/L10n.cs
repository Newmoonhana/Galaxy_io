using UnityEngine;
using UnityEngine.Localization;
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
        /// 표시 시점의 언어로 조회. <see cref="PreloadTables"/> 이후엔 캐시에서 동기 반환.
        /// </summary>
        public static string Get(string _key)
            => LocalizationSettings.StringDatabase.GetLocalizedString(tableName, _key);

        /// <summary>
        /// 모든 언어의 테이블을 비동기로 미리 올림. 웹은 동기 로드가 안 되므로 첫 <see cref="Get"/> 전에 필요함.
        /// 언어를 바꿔도 테이블이 이미 있어 바로 조회됨.
        /// </summary>
        public static async Awaitable PreloadTables()
        {
            await LocalizationSettings.InitializationOperation.Task;

            foreach (Locale locale in LocalizationSettings.AvailableLocales.Locales)
            {
                await LocalizationSettings.StringDatabase.GetTableAsync(tableName, locale).Task;
            }
        }
    }
}
