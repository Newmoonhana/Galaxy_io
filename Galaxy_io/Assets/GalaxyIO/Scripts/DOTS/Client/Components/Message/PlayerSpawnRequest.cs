using GalaxyIO.DOTS.Components.Singleton;
using Unity.Collections;
using Unity.Entities;
using Unity.Networking.Transport;

namespace GalaxyIO.DOTS.Client.Components.Message
{
    /// <summary>
    /// 게임 참가 의사 표시. 게임 시작 버튼을 누르면 생성되고,
    /// <see cref="Systems.ClientConnectionSystem"/>이 처리를 끝내면 제거함.
    /// </summary>
    public struct PlayerSpawnRequest : IComponentData
    {
        // 접속할 서버 주소. 이미 접속된 뒤의 리스폰 요청은 비워 둠.
        public NetworkEndpoint Endpoint;

        // 플레이어 이름. 리스폰 요청은 이름을 바꿀 때만 채움.
        public FixedString64Bytes Name;

        // 팀 모드의 리스폰 요청이 탈 기종. 그 밖에는 None.
        public SpaceshipId Spaceship;

        // 입장만 하고 스폰하지 않음. 보내고 나면 리스폰 창(이름 입력 창)을 띄움.
        public bool Hold;
    }
}