using GalaxyIO.DOTS.Components.Singleton;
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

        // 팀 모드에서 탈 기종. 입장 요청은 None 이고, 서버는 리스폰 창을 띄우게 함.
        public SpaceshipId Spaceship;
    }
}