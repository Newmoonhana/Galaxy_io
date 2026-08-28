using Unity.Entities;
using Unity.Mathematics;

namespace GalaxyIO
{
    /// <summary>Linear velocity in world units per second. XY only; the sim is 2D.</summary>
    public struct Velocity : IComponentData
    {
        public float2 Value;
    }
}
