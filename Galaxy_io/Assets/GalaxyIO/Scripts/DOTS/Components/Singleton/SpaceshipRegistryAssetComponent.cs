using GalaxyIO.DOTS.Authoring.Config;
using Unity.Entities;

namespace GalaxyIO.DOTS.Components.Singleton
{
    /// <summary>
    /// 우주선 상점이 기종 정의를 찾는 <see cref="SpaceshipRegistryAsset"/>. 월드당 한 개.
    /// </summary>
    public sealed class SpaceshipRegistryAssetComponent : IComponentData
    {
        public SpaceshipRegistryAsset Value;
    }
}
