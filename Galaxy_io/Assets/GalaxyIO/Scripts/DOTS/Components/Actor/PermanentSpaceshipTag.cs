using GalaxyIO.DOTS.Systems;
using Unity.Entities;

namespace GalaxyIO.DOTS.Components.Actor
{
    /// <summary>
    /// 레이더 범위와 무관하게 유지되는 우주선 태그. <see cref="EnemyDespawnSystem"/>이 제거하지 않음.
    /// </summary>
    public struct PermanentSpaceshipTag : IComponentData
    {
    }
}
