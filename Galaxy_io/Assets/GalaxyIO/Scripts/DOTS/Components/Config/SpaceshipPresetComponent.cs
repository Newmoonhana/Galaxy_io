using GalaxyIO.DOTS.Authoring.Config;
using Unity.Entities;

namespace GalaxyIO.DOTS.Components.Config
{
    /// <summary>
    /// <see cref="SpaceshipPresetAuthoring"/>의 DOTS 구조체.
    /// </summary>
    public struct SpaceshipPresetComponent : IComponentData
    {
        // 플레이어 최초 스폰시의 기본 Spaceship
        public Entity PlayerDefaultShip;
    }
}