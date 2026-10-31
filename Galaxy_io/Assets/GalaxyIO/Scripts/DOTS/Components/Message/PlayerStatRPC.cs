using GalaxyIO.DOTS.Components.Singleton;
using Unity.Collections;
using Unity.NetCode;

namespace GalaxyIO.DOTS.Components.Message
{
    /// <summary>
    /// 팀 모드 점수판 한 줄. 서버가 바뀔 때마다 모든 접속에 보냄. 입장한 플레이어에게는 전체를 보냄.
    /// </summary>
    public struct PlayerStatRPC : IRpcCommand
    {
        public int NetworkId;

        // 퇴장이면 비어 있음.
        public FixedString64Bytes Name;

        public Team Team;

        // 타고 있거나 마지막으로 탄 기종. 아직 타지 않았으면 None.
        public SpaceshipId Spaceship;

        public ushort Kills;
        public ushort Assists;
        public ushort Deaths;

        // 응답 시간(ms)
        public ushort Ping;

        // 퇴장 여부
        public bool Left;
    }
}
