using GalaxyIO.DOTS.Authoring;
using Unity.Entities;

namespace GalaxyIO.DOTS.Components
{
    /// <summary>
    /// <see cref="StraightforwardProjectileAuthoring"/>의 DOTS 구조체. 공통된 Projectile의 정보 함유.
    /// </summary>
    public struct SharedProjectileComponent : IComponentData
    {
        // 남은 생명 주기
        public float Lifetime;

        // 공격력
        public float Damage;
    }
}