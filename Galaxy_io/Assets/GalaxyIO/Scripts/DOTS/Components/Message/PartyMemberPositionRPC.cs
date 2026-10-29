using Unity.Mathematics;
using Unity.NetCode;

namespace GalaxyIO.DOTS.Components.Message
{
    /// <summary>
    /// 파티원 우주선의 위치. 서버가 주기적으로 같은 파티원에게 보냄. 레이더 밖 파티원 표시에 씀.
    /// </summary>
    public struct PartyMemberPositionRPC : IRpcCommand
    {
        public int NetworkId;

        public float2 Position;

        // 파티원에게 공유하는 레이더 탐지 거리(강화 포함). 초계함이 아니면 0. 아래 값도 초계함만 채움.
        public float Range;

        // 레이더 스캐너 기본 회전 속도(도/초)와 강화로 더한 값
        public float SweepSpeed;
        public float SweepBonus;

        // 강화를 반영한 반응 시간(초)
        public float ResponseTime;
    }
}