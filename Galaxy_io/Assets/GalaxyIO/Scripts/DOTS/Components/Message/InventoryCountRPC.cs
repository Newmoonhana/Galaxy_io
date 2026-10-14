using GalaxyIO.DOTS.Components.Singleton;
using Unity.NetCode;

namespace GalaxyIO.DOTS.Components.Message
{
    /// <summary>
    /// 서버가 인벤토리 주인에게 바뀐 재료 개수 하나를 알리는 RPC.
    /// </summary>
    public struct InventoryCountRPC : IRpcCommand
    {
        public ItemId Id;

        public ushort Count;
    }
}
