using GalaxyIO.Authoring;
using Unity.Entities;

namespace GalaxyIO.Components
{
    /// <summary>
    /// <see cref="SpaceshipAuthoring"/>의 DOTS 구조체.
    ///
    /// </summary>
    public struct SpaceshipSpecComponent : IComponentData
    {
        // 최대 체력
        public float MaxHealth;

        // 자가 수리 속도
        public float Repair;

        // 현재 체력
        public float Health;
    }
}