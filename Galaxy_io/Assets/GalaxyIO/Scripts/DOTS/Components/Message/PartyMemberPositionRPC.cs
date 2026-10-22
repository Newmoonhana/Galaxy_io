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
    }
}