using GalaxyIO.DOTS.Authoring;
using Unity.Entities;

namespace GalaxyIO.DOTS.Components.Actor
{
    /// <summary>
    /// <see cref="EffectAuthoring"/>의 DOTS 구조체. 연출 엔티티의 남은 재생 시간.
    /// </summary>
    public struct EffectLifetimeComponent : IComponentData
    {
        // 남은 시간(초). 0 이하가 되면 엔티티와 Companion GameObject 를 함께 제거함.
        public float Remaining;
    }
}