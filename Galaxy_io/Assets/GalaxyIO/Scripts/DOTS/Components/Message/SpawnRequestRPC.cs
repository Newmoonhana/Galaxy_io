using Unity.Collections;
using Unity.NetCode;

namespace GalaxyIO.DOTS.Components.Message
{
    /// <summary>
    /// 클라이언트가 서버에 플레이어 스폰을 요청하는 RPC.
    /// </summary>
    public struct SpawnRequestRPC : IRpcCommand
    {
        // 플레이어 이름. 리스폰 요청은 비워 둠. 서버가 접속 시 받은 이름을 씀.
        public FixedString64Bytes Name;
    }
}