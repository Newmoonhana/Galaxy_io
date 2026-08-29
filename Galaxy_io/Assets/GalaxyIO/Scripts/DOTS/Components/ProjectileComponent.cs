using GalaxyIO.Authoring;
using Unity.Entities;

namespace GalaxyIO.Components
{
    /// <summary>
    /// <see cref="ProjectileAuthoring"/>의 DOTS 구조체
    /// </summary>
    public struct ProjectileComponent : IComponentData
    {
        public float Lifetime;
        public float Damage;
    }
}