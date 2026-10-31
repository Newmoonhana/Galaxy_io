using Unity.NetCode;

namespace GalaxyIO.DOTS.Components.Message
{
    /// <summary>
    /// 상대 팀으로 옮겨 달라는 요청. 점수판의 팀 변경 버튼이 보냄.
    /// </summary>
    public struct TeamSwitchRPC : IRpcCommand
    {
    }
}
