using Unity.Entities;

namespace GalaxyIO.DOTS.Client.Components
{
    /// <summary>
    /// 게임 참가 의사 표시. 게임 시작 버튼을 누르면 생성되고,
    /// <see cref="Systems.ClientConnectionSystem"/>이 처리를 끝내면 제거함.
    /// </summary>
    public struct PlayerSpawnRequest : IComponentData
    {
    }
}
