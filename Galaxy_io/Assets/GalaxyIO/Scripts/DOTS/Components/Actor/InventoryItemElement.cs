using GalaxyIO.DOTS.Components.Singleton;
using Unity.Entities;

namespace GalaxyIO.DOTS.Components.Actor
{
    /// <summary>
    /// 플레이어가 주운 아이템. 같은 아이템은 한 칸에 합쳐짐. 서버만 채움.
    /// </summary>
    [InternalBufferCapacity(0)]
    public struct InventoryItemElement : IBufferElementData
    {
        public ItemId Id;

        public int Count;
    }
}
