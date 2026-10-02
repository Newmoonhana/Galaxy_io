using GalaxyIO.DOTS.Authoring.Config;
using Unity.Entities;

namespace GalaxyIO.DOTS.Components.Singleton
{
    /// <summary>
    /// 인벤토리, 제작 규칙이 아이템 정의를 찾는 <see cref="ItemRegistryAsset"/>. 월드당 한 개.
    /// </summary>
    public sealed class ItemRegistryAssetComponent : IComponentData
    {
        public ItemRegistryAsset Value;
    }
}
