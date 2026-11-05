using GalaxyIO.DOTS.Components.Actor;
using Unity.NetCode;

namespace GalaxyIO.DOTS.Components.Message
{
    /// <summary>
    /// 서버가 스폰 요청의 이름을 거절했다는 알림. 받으면 경고창을 띄우고 리스폰 창을 다시 엶.
    /// </summary>
    public struct PlayerNameRejectedRPC : IRpcCommand
    {
        public PlayerNameError Error;
    }
}
