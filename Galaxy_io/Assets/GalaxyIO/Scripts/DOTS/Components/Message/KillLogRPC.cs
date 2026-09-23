using GalaxyIO.DOTS.Components.Actor;
using Unity.Collections;
using Unity.NetCode;

namespace GalaxyIO.DOTS.Components.Message
{
    /// <summary>
    /// 플레이어가 격추됐음을 서버가 모든 클라이언트에게 알리는 RPC.
    /// </summary>
    public struct KillLogRPC : IRpcCommand
    {
        // 막타. 플레이어 이름 또는 AI 표시 이름. 알 수 없으면 비어 있음.
        public FixedString64Bytes Killer;

        // 어시스트. 없으면 비어 있음.
        public FixedString64Bytes Assistant;

        // 격추된 플레이어
        public FixedString64Bytes Victim;

        // 0 이면 AI 가 막타이거나 알 수 없음.
        public int KillerNetworkId;

        // 0 이면 어시스트 없음.
        public int AssistantNetworkId;

        public int VictimNetworkId;

        public KillType Type;
    }
}
