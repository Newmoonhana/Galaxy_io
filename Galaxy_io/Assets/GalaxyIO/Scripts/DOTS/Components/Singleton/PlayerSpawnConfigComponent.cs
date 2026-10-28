using GalaxyIO.DOTS.Authoring.Config;
using Unity.Entities;

namespace GalaxyIO.DOTS.Components.Singleton
{
    /// <summary>
    /// <see cref="PlayerSpawnConfigAuthoring"/>의 DOTS 구조체. 월드당 한 개.
    /// </summary>
    public struct PlayerSpawnConfigComponent : IComponentData
    {
        // 최초 스폰 / 리스폰 때 타는 우주선
        public SpaceshipId DefaultSpaceship;
    }
}
