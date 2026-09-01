using System.Collections.Generic;
using GalaxyIO.Components;
using GalaxyIO.Components.Tags;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;

namespace GalaxyIO.Authoring
{
    public sealed class SpaceshipAuthoring : MonoBehaviour
    {
        [Header("Attributes")]

        // 월드 좌표 상 이동속도
        public float Speed;

        // 가속도
        public float Acceleration;

        // 회전 속도(Degree/초)
        public float Maneuver;

        // 고속 선회 억제: 최대 속력일 때 회전 속도가 깎이는 비율.
        [Range(0.0F, 1.0F)]
        public float ManeuverPenalty = 0.6F;

        // 선회 제동: 회전한 각도(Radian)당 깎이는 속력 비율.
        [Min(0.0F)]
        public float TurnBrake = 0.5F;

        // 최대 체력
        public float MaxHealth;

        // 자가 수리 속도
        public float Repair;

        [Header("Turrets")]

        // 터렛 List
        public List<Transform> Turrets;

        [Header("Colliders")]

        // 충돌체 크기
        public float Size;

        [Header("Tags")]

        public uint TagFlag;
    }

    public sealed class SpaceshipBaker : Baker<SpaceshipAuthoring>
    {
        public override void Bake(SpaceshipAuthoring authoring)
        {
            Entity entity = GetEntity(TransformUsageFlags.Dynamic);

            AddComponent(entity, new SpaceshipSpecComponent
            {
                MaxHealth = authoring.MaxHealth,
                Repair    = authoring.Repair,
                Health    = authoring.MaxHealth,
            });

            AddComponent(entity, new SpaceshipMovementComponent
            {
                MaxSpeed        = authoring.Speed,
                Speed           = 0.0F,
                Acceleration    = authoring.Acceleration,
                Maneuver        = authoring.Maneuver,
                ManeuverPenalty = authoring.ManeuverPenalty,
                TurnBrake       = authoring.TurnBrake,
                Forward         = new float2(1.0F, 0.0F)
            });

            var buffer = AddBuffer<TurretElement>(entity);

            // (클래스의 참조가 아님. Entity의 Index로 참조)
            foreach (Transform t in authoring.Turrets)
            {
                Turret turret = t.GetComponent<Turret>();

                buffer.Add(new TurretElement
                {
                    MuzzleLocation = LocalTransform.FromPositionRotationScale(
                        t.localPosition,
                        t.localRotation,
                        t.localScale.x
                    ),
                    FireCooldown     = 0.0F,
                    MaxFireCooldown  = (turret == null || turret.Projectile == null) ? 0.0F : 1.0F / turret.RPS,
                    Spread           = (turret == null || turret.Projectile == null) ? 0.0F : (1.0F - turret.Accuracy) * math.TORADIANS * 90.0F,
                    ProjectilePrefab = (turret == null || turret.Projectile == null)
                        ? Entity.Null : GetEntity(turret.Projectile, TransformUsageFlags.Dynamic)
                });
            }

            if ((authoring.TagFlag | SpaceshipTags.LOCAL_PLAYER) == 1)
            {
                AddComponent(entity, new LocalPlayerTag());
            }
        }
    }
}