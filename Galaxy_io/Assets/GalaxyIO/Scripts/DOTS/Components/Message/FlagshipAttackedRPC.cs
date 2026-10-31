using Unity.NetCode;

namespace GalaxyIO.DOTS.Components.Message
{
    /// <summary>
    /// 우리 팀 기함(대장선)이 상대 팀에게 공격받는 중. 서버가 그 팀 플레이어에게 일정 간격으로 보냄.
    /// 클라이언트는 경보를 울리고 화면 가운데에 알림을 짧게 띄움.
    /// </summary>
    public struct FlagshipAttackedRPC : IRpcCommand
    {
    }
}
