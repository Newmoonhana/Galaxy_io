using System;
using GalaxyIO.DOTS.Components.Actor;
using UnityEngine;

namespace GalaxyIO.Tools
{
    /// <summary>
    /// 웹 페이지(index.html)에서 입력한 닉네임. 페이지가 주소의 ?name= 에 넣어 둠.
    /// 없거나 비어 있으면 null.
    /// </summary>
    public static class WebPlayerName
    {
        public static string FromPage()
        {
            if (!Uri.TryCreate(Application.absoluteURL, UriKind.Absolute, out Uri uri) || string.IsNullOrEmpty(uri.Query))
            {
                return null;
            }

            foreach (string pair in uri.Query.TrimStart('?').Split('&'))
            {
                int equals = pair.IndexOf('=');

                if (equals <= 0 || !string.Equals(pair.Substring(0, equals), "name", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                string name;

                try
                {
                    name = Uri.UnescapeDataString(pair.Substring(equals + 1).Replace('+', ' ')).Trim();
                }
                catch (Exception)
                {
                    return null;
                }

                // 줄바꿈 등 제어 문자는 뺌.
                var cleaned = new System.Text.StringBuilder(name.Length);

                foreach (char c in name)
                {
                    if (!char.IsControl(c))
                    {
                        cleaned.Append(c);
                    }
                }

                name = cleaned.ToString().Trim();

                if (name.Length > PlayerNameComponent.MaxLength)
                {
                    name = name.Substring(0, PlayerNameComponent.MaxLength);
                }

                return name.Length > 0 ? name : null;
            }

            return null;
        }
    }
}
