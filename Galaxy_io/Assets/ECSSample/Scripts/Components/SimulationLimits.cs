using Unity.Entities;

namespace GalaxyIO
{
    /// <summary>Singleton. Authoring-time guard rails the HUD clamps user input against.</summary>
    public struct SimulationLimits : IComponentData
    {
        public int HardMaxBotCount;
    }
}
