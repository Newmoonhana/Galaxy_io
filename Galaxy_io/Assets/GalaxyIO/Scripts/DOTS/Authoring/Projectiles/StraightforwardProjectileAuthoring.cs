using GalaxyIO.DOTS.Components.Actor;
using Unity.Entities;
using UnityEngine;

namespace GalaxyIO.DOTS.Authoring
{
    /// <summary>
    /// 직선으로 나아가는 투사체의 설정값.
    /// </summary>
    public sealed class StraightforwardProjectileAuthoring : ProjectileAuthoring
    {
        [SerializeField]
        [Tooltip("투사체 최초 속도")]
        public float Speed;

        [SerializeField]
        [Tooltip("투사체 가속도 (음수 값 가능. 점점 느려짐.)")]
        public float Acceleration;

        [SerializeField]
        [Tooltip("발사한 우주선의 속력을 최초 속도에 더함. 기수 방향으로 나가는 투사체만 켤 것.")]
        public bool ReflectOwnerVelocity = true;
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
                Acceleration = authoring.Acceleration,
                Speed = authoring.Speed,
                ReflectOwnerVelocity = authoring.ReflectOwnerVelocity,
            });

            // 예측 스폰 짝짓기용 식별자. 값은 발사 시점에 채움.
            AddComponent<ProjectileSpawnIdComponent>(entity);

            // 쏜 우주선의 진영. 값은 발사 시점에 채움.
            AddComponent(entity, new FactionComponent
            {
                Value = FactionID.None,
            });

            AddBuffer<ProjectileHitElement>(entity);

            // 켜진 채로 스폰됨. 클라이언트가 발사음 재생 후 끔.
            AddComponent<ShotSoundPendingTag>(entity);
        }
    }
}