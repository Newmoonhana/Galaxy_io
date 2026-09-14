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
    }
}