using Unity.Mathematics;
using Unity.NetCode;

namespace GalaxyIO.DOTS.Components.Message
{
    /// <summary>
    /// 우주선이 파괴됐음을 서버가 근처 클라이언트와 소유자에게 알리는 RPC.
    /// </summary>
    public struct SpaceshipDestroyedRPC : IRpcCommand
    {
        // 폭발 연출을 띄울 위치
        public float2 Position;

        // 파괴된 우주선의 소유자. 자기 것이면 리스폰 창을 엶.
        public int OwnerNetworkId;
    }
}