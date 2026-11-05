using Unity.Entities;

namespace GalaxyIO
{
    /// <summary>Singleton. Read-only mirror of the sim for the HUD.</summary>
    public struct SimulationStats : IComponentData
    {
        public int AliveBotCount;
        public int DestroyedBotCount;
        public int DestroyedThisFrame;
    }
}
