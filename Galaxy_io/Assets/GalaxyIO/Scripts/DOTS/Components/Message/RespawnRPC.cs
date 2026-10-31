using GalaxyIO.DOTS.Components.Singleton;
using Unity.Collections;
using Unity.NetCode;

namespace GalaxyIO.DOTS.Components.Message
{
    /// <summary>
    /// 팀 모드에서 부활할 기체를 고르라는 알림. 입장, 격추, 팀 변경, 부활 거절 때 서버가 그 플레이어에게만 보냄.
    /// 받으면 리스폰 창이 열림.
    /// </summary>
    public struct RespawnRPC : IRpcCommand
    {
        public Team Team;

        // 부활할 수 있기까지 남은 시간(초)
        public float Delay;

        // 격추한 쪽 이름. 격추가 아니면 비어 있음.
        public FixedString64Bytes Killer;
    }
}
