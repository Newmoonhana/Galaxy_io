using System.Collections.Generic;

namespace OOPSample
{
    /// <summary>
    /// 살아 있는 봇의 목록.
    ///
    /// DOTS에서는 <c>EntityQuery</c>가 이 역할을 공짜로 해 줍니다(청크를 순회하면 그게 곧 목록).
    /// GameObject 쪽에는 그런 게 없으므로 직접 관리해야 하고, 이 클래스가 바로 그 비용입니다.
    /// <c>FindObjectsOfType</c> 같은 건 프레임마다 쓸 물건이 아니므로 스폰/디스폰 시점에
    /// 리스트를 갱신합니다.
    ///
    /// 제거는 O(1) swap-back입니다. 봇이 자기 인덱스를 들고 있기 때문에 <c>List.Remove</c>의
    /// 선형 탐색을 피할 수 있습니다. 순서가 보장되지 않지만 시뮬레이션은 순서에 의존하지 않습니다.
    /// </summary>
    public sealed class BotRegistry
    {
        private readonly List<BotBehaviour> m_Bots = new List<BotBehaviour>(4096);

        public int Count => m_Bots.Count;

        public BotBehaviour this[int index] => m_Bots[index];

        public void Add(BotBehaviour bot)
        {
            bot.RegistryIndex = m_Bots.Count;
            m_Bots.Add(bot);
        }

        public void Remove(BotBehaviour bot)
        {
            var index = bot.RegistryIndex;
            if (index < 0 || index >= m_Bots.Count || m_Bots[index] != bot)
                return;

            var last = m_Bots.Count - 1;
            var moved = m_Bots[last];
            m_Bots[index] = moved;
            moved.RegistryIndex = index;
            m_Bots.RemoveAt(last);

            bot.RegistryIndex = -1;
        }

        /// <summary>목록의 꼬리에서 하나 꺼내 반환합니다. 개체수 상한이 내려갔을 때의 잉여분 정리용.</summary>
        public BotBehaviour PopLast()
        {
            var last = m_Bots.Count - 1;
            var bot = m_Bots[last];
            m_Bots.RemoveAt(last);
            bot.RegistryIndex = -1;
            return bot;
        }
    }
}
