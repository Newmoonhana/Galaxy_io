using GalaxyIO.Components;
using GalaxyIO.Components.Tags;
using Unity.Entities;
using Unity.Mathematics;
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

        [Header("Colliders")]

        // 충돌체 크기
        public float Size;

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

            AddComponent(entity, new MovementComponent
            {
                MaxSpeed        = authoring.Speed,
                Speed           = 0.0F,
                Acceleration    = authoring.Acceleration,
                Maneuver        = authoring.Maneuver,
                ManeuverPenalty = authoring.ManeuverPenalty,
                TurnBrake       = authoring.TurnBrake,
                Forward         = new float2(1.0F, 0.0F)
            });

            if ((authoring.TagFlag | SpaceshipTags.LOCAL_PLAYER) == 1)
            {
                AddComponent(entity, new LocalPlayerTag());
            }
        }
    }
}