using GalaxyIO.Components;
using GalaxyIO.Components.Projectiles;
using Unity.Entities;
using UnityEngine;

namespace GalaxyIO.Authoring
{
    public sealed class StraightforwardProjectileAuthoring : ProjectileAuthoring
    {
        [SerializeField]
        [Tooltip("투사체 최초 속도")]
        public float Speed;

        [SerializeField]
        [Tooltip("투사체 가속도 (음수 값 가능. 점점 느려짐.)")]
        public float Acceleration;
    }

    public sealed class StraightforwardProjectileBaker : Baker<StraightforwardProjectileAuthoring>
    {
        public override void Bake(StraightforwardProjectileAuthoring authoring)
        {
            Entity entity = GetEntity(TransformUsageFlags.Dynamic); // LocalTransform 컴포넌트 추가

            AddComponent(entity, new SharedProjectileComponent
            {
                Lifetime = authoring.Lifetime,
                Damage = authoring.Damage,
            });

            AddComponent(entity, new StraightforwardProjectileComponent
            {
                acceleration = authoring.Acceleration,
                speed = authoring.Speed,
            });
        }
    }
}