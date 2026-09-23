using Unity.Mathematics;
using Unity.NetCode;

namespace GalaxyIO.DOTS.Components.Message
{
    /// <summary>
    /// 우주선끼리 부딪혔음을 서버가 부딪힌 플레이어 본인에게 알리는 RPC.
    /// </summary>
    public struct SpaceshipCrashedRPC : IRpcCommand
    {
        // 충돌음을 낼 위치. 두 우주선의 중간.
        public float2 Position;
    }
}
