using Unity.Entities;

namespace GalaxyIO.DOTS.Client.Components.Message
{
    /// <summary>
    /// 로컬 플레이어의 우주선이 파괴된 상태임을 나타내는 태그.
    /// 리스폰 창이 이 태그를 보고 열리고, 리스폰을 요청하면 제거됨.
    /// </summary>
    public struct PlayerDeathTag : IComponentData
    {
    }
}