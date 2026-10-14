using GalaxyIO.DOTS.Components.Singleton;
using Unity.Entities;

namespace GalaxyIO.DOTS.Components.Actor
{
    /// <summary>
    /// 우주선의 기종. 프리팹에 베이킹되므로 복제하지 않음.
    /// </summary>
    public struct SpaceshipKindComponent : IComponentData
    {
        public SpaceshipId Value;
    }
}
