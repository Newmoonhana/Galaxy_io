using GalaxyIO.DOTS.Authoring;
using Unity.Entities;

namespace GalaxyIO.DOTS.Components.Actor
{
    /// <summary>
    /// <see cref="ProjectileAuthoring.Visual"/>의 DOTS 구조체. 투사체를 따라다니는 파티클/트레일 연출. 클라이언트만 씀.
    /// </summary>
    public struct ProjectileVisualComponent : IComponentData
    {
        // 연출 프리팹
        public Entity Prefab;

        // 스폰한 연출. 아직 스폰하지 않았으면 Entity.Null.
        public Entity Instance;
    }

    /// <summary>
    /// 투사체를 따라다니는 연출 쪽에 붙음. 투사체가 사라지거나 감춰지면 연출도 지움.
    /// </summary>
    public struct ProjectileVisualOwnerComponent : IComponentData
    {
        public Entity Projectile;
    }
}
