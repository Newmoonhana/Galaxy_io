using GalaxyIO.DOTS.Inventory;
using Unity.Entities;

namespace GalaxyIO.DOTS.Client.Components.Singleton
{
    /// <summary>
    /// 서버가 보낸 로컬 플레이어 인벤토리 사본. 클라이언트 월드당 한 개.
    /// </summary>
    public sealed class LocalInventoryComponent : IComponentData
    {
        // 첫 칸을 받기 전에는 null
        public PlayerInventory Value;
    }
}
