using GalaxyIO.DOTS.Components.Actor;
using Unity.NetCode;

namespace GalaxyIO.DOTS.Components.Message
{
    /// <summary>
    /// 클라이언트가 파워 셀을 써서 자기 우주선의 능력치를 한 단계 올려 달라고 요청하는 RPC.
    /// </summary>
    public struct UpgradeRPC : IRpcCommand
    {
        public UpgradeStat Stat;
    }
}
