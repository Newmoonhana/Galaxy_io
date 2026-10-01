using Unity.NetCode;

namespace GalaxyIO.DOTS.Components.Message
{
    /// <summary>
    /// 클라이언트가 자기 인벤토리의 설계도로 결과물을 하나 제작해 달라고 요청하는 RPC.
    /// </summary>
    public struct CraftRPC : IRpcCommand
    {
        // 설계도가 든 칸 번호
        public byte Slot;
    }
}
