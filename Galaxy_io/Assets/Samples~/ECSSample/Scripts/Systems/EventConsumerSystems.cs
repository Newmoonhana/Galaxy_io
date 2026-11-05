using Unity.Burst;
using Unity.Collections;
using Unity.Entities;

namespace GalaxyIO
{
    /// <summary>
    /// CONSUMER #1.
    ///
    /// Reads <see cref="BotHitPlayerEvent"/> messages and destroys the bots they name.
    /// It has no reference to the producer and no reference to the other consumer.
    ///
    /// The destroy happens on the main thread because the message count per frame is tiny
    /// (one per touching bot) and <c>EntityManager.DestroyEntity</c> on a batch is the
    /// cheapest way to do it. <c>Exists</c> guards against a bot that some other system
    /// already removed this frame - messages are snapshots, they can go stale.
    /// </summary>
    [UpdateInGroup(typeof(EventConsumerSystemGroup))]
    public partial struct BotDespawnSystem : ISystem
    {
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate(SystemAPI.QueryBuilder().WithAll<BotHitPlayerEvent>().Build());
        }

        public void OnUpdate(ref SystemState state)
        {
            var victims = new NativeList<Entity>(64, Allocator.Temp);

            foreach (var message in SystemAPI.Query<RefRO<BotHitPlayerEvent>>())
            {
                var bot = message.ValueRO.Bot;
                if (state.EntityManager.Exists(bot))
                    victims.Add(bot);
            }

            if (victims.Length > 0)
                state.EntityManager.DestroyEntity(victims.AsArray());

            victims.Dispose();
        }
    }

    /// <summary>
    /// CONSUMER #2.
    ///
    /// Reads exactly the same messages as <see cref="BotDespawnSystem"/> and updates the
    /// counters the HUD displays. Two unrelated reactions to one event, neither aware of
    /// the other: that is the payoff of routing through messages instead of having the
    /// collision code call a Destroy() and a Score++ directly.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(EventConsumerSystemGroup))]
    public partial struct ScoreSystem : ISystem
    {
        EntityQuery m_Messages;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            m_Messages = SystemAPI.QueryBuilder().WithAll<BotHitPlayerEvent>().Build();
            state.RequireForUpdate(m_Messages);
            state.RequireForUpdate<SimulationStats>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var hits = m_Messages.CalculateEntityCount();

            ref var stats = ref SystemAPI.GetSingletonRW<SimulationStats>().ValueRW;
            stats.DestroyedThisFrame = hits;
            stats.DestroyedBotCount += hits;
        }
    }

    /// <summary>
    /// Destroys every event entity at the end of the frame, giving messages a strict
    /// one-frame lifetime: a consumer either handles a message in the frame it was
    /// produced or never sees it. Without this the world would fill up with dead messages.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateAfter(typeof(EventConsumerSystemGroup))]
    public partial struct EventCleanupSystem : ISystem
    {
        EntityQuery m_Events;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            m_Events = SystemAPI.QueryBuilder().WithAll<SimulationEvent>().Build();
            state.RequireForUpdate(m_Events);
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            state.EntityManager.DestroyEntity(m_Events);
        }
    }
}