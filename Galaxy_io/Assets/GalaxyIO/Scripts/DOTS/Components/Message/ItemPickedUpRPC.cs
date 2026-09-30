using Unity.Mathematics;
using Unity.NetCode;

namespace GalaxyIO.DOTS.Components.Message
{
    /// <summary>
    /// 아이템을 주웠음을 서버가 주운 플레이어 본인에게 알리는 RPC.
    /// </summary>
    public struct ItemPickedUpRPC : IRpcCommand
    {
        // 효과음을 낼 위치. 주운 아이템의 위치.
        public float2 Position;
    }
}
