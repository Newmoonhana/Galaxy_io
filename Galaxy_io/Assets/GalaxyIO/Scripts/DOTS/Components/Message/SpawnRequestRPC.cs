using Unity.NetCode;

namespace GalaxyIO.DOTS.Components.Message
{
    /// <summary>
    /// 클라이언트가 서버에 플레이어 스폰을 요청하는 RPC.
    /// </summary>
    public struct SpawnRequestRPC : IRpcCommand
    {
    }
}