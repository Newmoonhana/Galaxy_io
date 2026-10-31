using GalaxyIO.DOTS.Authoring;
using GalaxyIO.DOTS.Components.Blob;
using GalaxyIO.DOTS.Components.Singleton;
using Unity.Entities;

namespace GalaxyIO.DOTS.Components.Actor
{
    /// <summary>
    /// <see cref="DropTableAuthoring"/>의 DOTS 구조체. 테이블은 <see cref="DropTableRegistryElement"/>에서 찾음.
    /// </summary>
    public struct DropTableComponent : IComponentData
    {
        public EnemyType Enemy;
    }
}
