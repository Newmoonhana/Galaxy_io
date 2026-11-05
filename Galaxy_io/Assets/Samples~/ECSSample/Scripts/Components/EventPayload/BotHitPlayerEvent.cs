using Unity.Entities;
using Unity.Mathematics;

namespace GalaxyIO
{
    /// <summary>
    /// Message payload: "bot X overlapped the player this frame".
    /// Produced by <c>CollisionDetectionSystem</c>, consumed by <c>BotDespawnSystem</c>
    /// and <c>ScoreSystem</c>. Paired with <see cref="SimulationEvent"/> on the event
    /// entity; see that file for the event-entity pattern this project uses.
    /// </summary>
    public struct BotHitPlayerEvent : IComponentData
    {
        public Entity Bot;
        public Entity Player;
        public float2 Position;
    }
}
