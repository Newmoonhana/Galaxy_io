using GalaxyIO.DOTS.Components.Singleton;
using Unity.NetCode;

namespace GalaxyIO.DOTS.Components.Message
{
    /// <summary>
    /// 우리 팀의 분류별 기체 수. 서버가 바뀔 때마다 그 팀에게 보냄. 리스폰 창의 분류 버튼이 씀.
    /// </summary>
    public struct TeamSlotRPC : IRpcCommand
    {
        public SpaceshipCategory Category;

        // 살아 있는 기체와 자리가 아직 비지 않은 격추 기체를 합친 수
        public byte Occupied;

        public byte Max;
    }
}
