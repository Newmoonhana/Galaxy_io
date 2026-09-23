using Unity.Collections;
using Unity.NetCode;

namespace GalaxyIO.DOTS.Components.Message
{
    /// <summary>
    /// 플레이어 점수 변경, 입장, 퇴장을 클라이언트에 알리는 RPC.
    /// </summary>
    public struct PlayerScoreRPC : IRpcCommand
    {
        // 퇴장이면 비어 있음.
        public FixedString64Bytes Name;

        public int NetworkId;

        public int Score;

        // 퇴장 여부
        public bool Left;
    }
}
