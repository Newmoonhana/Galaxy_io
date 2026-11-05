using Unity.Entities;
using Unity.Mathematics;

namespace GalaxyIO
{
    /// <summary>Singleton. The rectangle every unit is confined to.</summary>
    public struct WorldBounds : IComponentData
    {
        public float2 Min;
        public float2 Max;
    }
}
