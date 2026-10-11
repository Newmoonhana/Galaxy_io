using System;
using System.Collections.Generic;

namespace GalaxyIO.Scripts.Managed.Chat
{
    /// <summary>
    /// 채팅창에 입력한 명령어를 이름으로 찾아 실행하는 목록.
    /// </summary>
    public sealed class ChatCommands
    {
        // 명령어는 항상 이 글자로 시작함.
        public const char Prefix = '/';

        private readonly Dictionary<string, Action<string>> handlers =
            new Dictionary<string, Action<string>>(StringComparer.OrdinalIgnoreCase);

        /// <summary>
        /// 명령어 등록. 핸들러는 명령어 이름 뒤의 문자열을 받음.
        /// </summary>
        public void Register(string name, Action<string> handler)
        {
            handlers[name] = handler;
        }

        /// <summary>
        /// <see cref="Prefix"/>로 시작하는 입력을 실행함.
        /// </summary>
        /// <returns>등록된 명령어인지 여부.</returns>
        public bool Execute(string input)
        {
            int space = input.IndexOf(' ');
            string name = space < 0 ? input.Substring(1) : input.Substring(1, space - 1);

            if (!handlers.TryGetValue(name, out Action<string> handler))
            {
                return false;
            }

            handler(space < 0 ? string.Empty : input.Substring(space + 1).Trim());
            return true;
        }
    }
}
