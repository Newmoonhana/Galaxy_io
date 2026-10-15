using Unity.NetCode;

namespace GalaxyIO.DOTS.Components.Message
{
    /// <summary>
    /// 파티에 들어가거나 나왔음을 알림. 서버가 해당 플레이어에게 보내고, 클라이언트는 파티 채팅 탭을 열고 닫음.
    /// </summary>
    public struct PartyStateRPC : IRpcCommand
    {
        public bool InParty;
    }
}