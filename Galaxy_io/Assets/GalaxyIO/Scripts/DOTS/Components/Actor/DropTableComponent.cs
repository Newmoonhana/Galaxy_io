using GalaxyIO.DOTS.Authoring;
using GalaxyIO.DOTS.Components.Blob;
using Unity.Entities;

namespace GalaxyIO.DOTS.Components.Actor
{
    /// <summary>
    /// <see cref="DropTableAuthoring"/>의 DOTS 구조체. 같은 프리팹의 인스턴스끼리 테이블을 공유함.
    /// </summary>
    public struct DropTableComponent : IComponentData
    {
        public BlobAssetReference<DropTableBlob> Value;
    }
}
