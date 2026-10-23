using Unity.Mathematics;
using Unity.NetCode;

namespace GalaxyIO.DOTS.Components.Message
{
    /// <summary>
    /// 우주선이 파괴됐음을 서버가 근처 클라이언트, 소유자, 막타를 친 플레이어에게 알리는 RPC.
    /// </summary>
    public struct SpaceshipDestroyedRPC : IRpcCommand
    {
        // 폭발 연출을 띄울 위치
        public float2 Position;

        // 파괴된 우주선의 소유자. 자기 것이면 리스폰 창을 엶.
        public int OwnerNetworkId;

        // 막타를 친 플레이어. 자기 것이면 폭발음을 최소 음량을 지켜 냄. AI, 알 수 없음은 0 이하.
        public int KillerNetworkId;
    }
}