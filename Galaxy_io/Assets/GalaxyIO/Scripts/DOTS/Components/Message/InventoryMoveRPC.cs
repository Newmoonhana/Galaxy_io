using Unity.NetCode;

namespace GalaxyIO.DOTS.Components.Message
{
    /// <summary>
    /// 클라이언트가 자기 인벤토리의 아이템을 다른 칸으로 옮겨 달라고 요청하는 RPC.
    /// 대상 칸에 다른 아이템이 있으면 개수와 상관없이 맞바꿈.
    /// </summary>
    public struct InventoryMoveRPC : IRpcCommand
    {
        // 칸 번호
        public byte From;
        public byte To;

        // 옮길 최대 개수. byte.MaxValue 면 묶음 전체.
        public byte Count;
    }
}
