using UnityEngine;

namespace OOPSample
{
    /// <summary>
    /// ECS 샘플의 <c>BotSpawnSystem</c> 대응.
    ///
    /// 봇 개체수를 <see cref="SimulationConfig.MaxBotCount"/>에 맞춰 유지합니다. HUD가 상한을
    /// 올렸거나 플레이어가 봇을 잡아먹었으면 채우고, 상한이 내려갔으면 잉여분을 정리합니다.
    /// 1000 -> 100000 같은 점프가 한 프레임을 통째로 잡아먹지 않도록 프레임당 생성량을 제한합니다.
    ///
    /// 각 봇은 스폰 시점에 단위원에서 균등하게 뽑은 방향과 무작위 속력을 받고, 그 뒤로 다시
    /// 뽑는 일은 없습니다. 방향은 스폰 때 한 번 정해집니다.
    ///
    /// DOTS와의 실질적 차이: <c>EntityManager.Instantiate(prefab, count, allocator)</c>는
    /// 청크를 통째로 복제하는 배치 연산이지만, 여기서는 <c>Object.Instantiate</c>를 count번
    /// 호출해야 합니다. GameObject 하나마다 Transform 등록과 매니지드 할당이 따라붙습니다.
    /// </summary>
    public sealed class BotSpawner
    {
        private readonly SimulationConfig m_Config;
        private readonly SimulationStats m_Stats;
        private readonly SimulationEventBus m_Bus;
        private readonly BotRegistry m_Registry;
        private readonly PlayerSpawner m_PlayerSpawner;
        private readonly WorldBounds m_Bounds;
        private readonly Transform m_Parent;

        private SimulationRandom m_Random;

        public BotSpawner(
            SimulationConfig config,
            SimulationStats stats,
            SimulationEventBus bus,
            BotRegistry registry,
            PlayerSpawner playerSpawner,
            in WorldBounds bounds,
            Transform parent,
            uint randomSeed)
        {
            m_Config = config;
            m_Stats = stats;
            m_Bus = bus;
            m_Registry = registry;
            m_PlayerSpawner = playerSpawner;
            m_Bounds = bounds;
            m_Parent = parent;
            m_Random = new SimulationRandom(randomSeed);
        }

        public void Tick()
        {
            var player = m_PlayerSpawner.Player;
            var alive = m_Registry.Count;
            var deficit = m_Config.MaxBotCount - alive;

            if (deficit > 0 && m_Config.BotPrefab != null && player != null)
            {
                var count = Mathf.Min(deficit, Mathf.Max(1, m_Config.MaxSpawnsPerFrame));
                Spawn(count, player);
                alive += count;
            }
            else if (deficit < 0)
            {
                var surplus = Mathf.Min(-deficit, m_Registry.Count);
                Cull(surplus);
                alive -= surplus;
            }

            m_Stats.AliveBotCount = alive;
        }

        private void Spawn(int count, PlayerBehaviour player)
        {
            var half = m_Config.BotSize * 0.5f;
            var min = m_Bounds.Min + new Vector2(half, half);
            var max = m_Bounds.Max - new Vector2(half, half);
            var prefab = m_Config.BotPrefab;

            for (var i = 0; i < count; i++)
            {
                var position = m_Random.NextFloat2(min, max);
                var angle = m_Random.NextFloat(0f, 2f * Mathf.PI);
                var speed = m_Random.NextFloat(m_Config.BotSpeedMin, m_Config.BotSpeedMax);
                var direction = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));

                var bot = Object.Instantiate(prefab, m_Parent);
                bot.Initialize(m_Bus, player, m_Bounds, m_Config.BotSize, position, direction * speed);
                m_Registry.Add(bot);
            }
        }

        private void Cull(int surplus)
        {
            for (var i = 0; i < surplus; i++)
            {
                var bot = m_Registry.PopLast();
                bot.MarkDestroyed();
                Object.Destroy(bot.gameObject);
            }
        }
    }
}
