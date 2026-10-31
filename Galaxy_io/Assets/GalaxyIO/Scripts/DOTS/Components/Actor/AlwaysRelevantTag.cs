using GalaxyIO.DOTS.Systems;
using Unity.Entities;

namespace GalaxyIO.DOTS.Components.Actor
{
    /// <summary>
    /// 거리와 무관하게 모든 접속에 복제하는 Ghost 태그. <see cref="GhostRelevancySystem"/>이 읽음. e.g. 점령전 위성, 대장선
    /// </summary>
    public struct AlwaysRelevantTag : IComponentData
    {
    }
}
