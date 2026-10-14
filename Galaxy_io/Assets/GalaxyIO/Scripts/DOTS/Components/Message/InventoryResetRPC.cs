using Unity.NetCode;

namespace GalaxyIO.DOTS.Components.Message
{
    /// <summary>
    /// 서버가 새 인벤토리를 만들었음을 주인에게 알리는 RPC. 주인은 사본을 빈 인벤토리로 바꿈.
    /// 0 이 아닌 재료는 뒤이어 <see cref="InventoryCountRPC"/>로 옴.
    /// </summary>
    public struct InventoryResetRPC : IRpcCommand
    {
    }
}
