using GalaxyIO.DOTS.Authoring;
using Unity.Entities;

namespace GalaxyIO.DOTS.Components.Actor
{
    /// <summary>
    /// <see cref="EffectAuthoring.Effects"/>의 DOTS 구조체. 연출을 이루는 파티클/트레일 엔티티(자신 또는 자식).
    /// </summary>
    [InternalBufferCapacity(2)]
    public struct EffectPartElement : IBufferElementData
    {
        public Entity Value;
    }
}
