using Unity.Entities;

namespace GalaxyIO.DOTS.Client.Components
{
    /// <summary>
    /// "게임에 참가하고 싶다"는 의사 표시.
    /// 게임 시작 버튼을 누르면 생성되고, <see cref="Systems.ClientConnectionSystem"/>이
    /// 접속과 스폰 RPC 전송을 모두 끝내면 제거한다.
    /// </summary>
    public struct PlayerSpawnRequest : IComponentData
    {
    }
}
