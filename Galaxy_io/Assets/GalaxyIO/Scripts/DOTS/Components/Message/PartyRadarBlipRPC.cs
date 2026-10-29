using Unity.Mathematics;
using Unity.NetCode;

namespace GalaxyIO.DOTS.Components.Message
{
    /// <summary>
    /// 초계함 파티원의 레이더 탐지 거리 안의 우주선 하나. 서버가 <see cref="PartyMemberPositionRPC"/> 직후 같은 파티원에게 보냄.
    /// 받는 쪽이 초계함의 스캐너를 흉내 내 점으로 띄움.
    /// </summary>
    public struct PartyRadarBlipRPC : IRpcCommand
    {
        // 레이더를 공유한 초계함 주인의 NetworkId
        public int NetworkId;

        public float2 Position;

        // 0: AI, 1: 다른 플레이어와 그 함재기, 2: 같은 편
        public byte Kind;

        // 점 크기 계산용 선체 두께
        public float Thickness;
    }
}
