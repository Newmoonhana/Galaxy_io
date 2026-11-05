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
        // 플레이어 이름. 리스폰 요청에서 비워 두면 서버가 쓰던 이름을 씀. 채우면 이름을 바꿈.
        public FixedString64Bytes Name;

        // 팀 모드에서 탈 기종. 입장 요청은 None 이고, 서버는 리스폰 창을 띄우게 함.
        public SpaceshipId Spaceship;

        // 입장만 하고 스폰하지 않음. 웹 접속이 이름 입력 창(리스폰 창)으로 시작할 때 씀.
        public bool Hold;
    }
}