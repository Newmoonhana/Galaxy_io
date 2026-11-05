using Unity.Entities;
using Unity.Mathematics;

namespace GalaxyIO
{
    /// <summary>Singleton. Deterministic RNG shared by the spawn systems.</summary>
    public struct SimulationRandom : IComponentData
    {
        public Random Value;
    }
}
