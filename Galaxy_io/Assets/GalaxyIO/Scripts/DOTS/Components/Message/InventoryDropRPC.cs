using Unity.NetCode;

namespace GalaxyIO.DOTS.Components.Message
{
    /// <summary>
    /// 클라이언트가 자기 인벤토리 한 칸의 아이템을 버려 달라고 요청하는 RPC.
    /// </summary>
    public struct InventoryDropRPC : IRpcCommand
    {
        // 칸 번호
        public byte Slot;

        // 버릴 최대 개수. byte.MaxValue 면 묶음 전체.
        public byte Count;
    }
}
