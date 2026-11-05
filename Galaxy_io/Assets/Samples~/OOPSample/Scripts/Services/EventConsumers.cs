using System.Collections.Generic;
using UnityEngine;

namespace OOPSample
{
    /// <summary>
    /// CONSUMER #1. ECS 샘플의 <c>BotDespawnSystem</c> 대응.
    ///
    /// <see cref="BotHitPlayerEvent"/>를 읽고 거기에 적힌 봇을 파괴합니다. 프로듀서에 대한
    /// 참조도, 다른 컨슈머에 대한 참조도 없습니다.
    ///
    /// <see cref="BotBehaviour.IsAlive"/> 검사는 ECS 쪽 <c>EntityManager.Exists</c> 가드와
    /// 같은 목적입니다. 게다가 Unity의 <c>Destroy</c>는 프레임 끝에야 반영되므로, 플래그 없이는
    /// 이미 파괴 예약된 오브젝트가 살아 있는 것처럼 보입니다.
    /// </summary>
    public sealed class BotDespawner
    {
        private readonly BotRegistry m_Registry;
        private readonly SimulationEventBus m_Bus;

        public BotDespawner(SimulationEventBus bus, BotRegistry registry)
        {
            m_Bus = bus;
            m_Registry = registry;
            m_Bus.BotHitPlayer.Consumed += OnBotHitPlayer;
        }

        public void Dispose() => m_Bus.BotHitPlayer.Consumed -= OnBotHitPlayer;

        private void OnBotHitPlayer(IReadOnlyList<BotHitPlayerEvent> messages)
        {
            for (var i = 0; i < messages.Count; i++)
            {
                var bot = messages[i].Bot;

                if (bot == null || !bot.IsAlive)
                    continue;

                bot.MarkDestroyed();
                m_Registry.Remove(bot);
                Object.Destroy(bot.gameObject);
            }
        }
    }

    /// <summary>
    /// CONSUMER #2. ECS 샘플의 <c>ScoreSystem</c> 대응.
    ///
    /// <see cref="BotDespawner"/>와 정확히 같은 메시지를 읽고 HUD가 표시할 카운터를 갱신합니다.
    /// 하나의 이벤트에 대한 두 개의 반응이, 서로의 존재를 모른 채로 붙어 있습니다. 충돌 코드가
    /// Destroy()와 Score++를 직접 호출했다면 얻지 못했을 성질입니다.
    /// </summary>
    public sealed class ScoreKeeper
    {
        private readonly SimulationStats m_Stats;
        private readonly SimulationEventBus m_Bus;

        public ScoreKeeper(SimulationEventBus bus, SimulationStats stats)
        {
            m_Bus = bus;
            m_Stats = stats;
            m_Bus.BotHitPlayer.Consumed += OnBotHitPlayer;
        }

        public void Dispose() => m_Bus.BotHitPlayer.Consumed -= OnBotHitPlayer;

        private void OnBotHitPlayer(IReadOnlyList<BotHitPlayerEvent> messages)
        {
            m_Stats.DestroyedThisFrame = messages.Count;
            m_Stats.DestroyedBotCount += messages.Count;
        }
    }
}
