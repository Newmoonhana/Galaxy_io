using GalaxyIO.DOTS.Components.Actor;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

namespace GalaxyIO.DOTS.Authoring
{
    /// <summary>
    /// 모선이 내보내는 함재기의 정의. 기체 스펙은 <see cref="SpaceshipAuthoring"/>을 따르고, 비행 모드는 Speed, Acceleration,
    /// Deceleration, 호버링 모드는 Hovering 값을 씀.
    /// </summary>
    [RequireComponent(typeof(SpaceshipAuthoring))]
    public sealed class CarriedCraftAuthoring : MonoBehaviour
    {
        [Header("Perception")]

        [SerializeField]
        [Tooltip("적을 찾는 반지름. 시야각 없이 거리로만 찾음.")]
        [Min(0.0F)]
        public float DetectRadius = 20.0F;

        [SerializeField]
        [Tooltip("호위 중에는 모선에서 이보다 먼 적을 쫓지 않음.")]
        [Min(0.0F)]
        public float LeashRadius = 25.0F;

        [SerializeField]
        [Tooltip("진형을 벗어나 교전할 때 대상 선체 바깥과 두는 거리")]
        [Min(0.0F)]
        public float EngageDistance = 5.0F;

        [Header("Movement")]

        [SerializeField]
        [Tooltip("진형 자리와의 거리가 이보다 가까워지면 호버링 모드로 바꿈.")]
        [Min(0.0F)]
        public float HoverDistance = 2.0F;

        [SerializeField]
        [Tooltip("진형 자리와의 거리가 이보다 멀어지면 비행 모드로 바꿈.")]
        [Min(0.0F)]
        public float FlightDistance = 6.0F;

        [Header("Formation")]

        [SerializeField]
        [Tooltip("공격 진형 첫 고리와 대상 선체 바깥 사이 거리")]
        [Min(0.0F)]
        public float AttackRadius = 10.0F;

        [SerializeField]
        [Tooltip("공격 진형 고리 수. 자리마다 이 중 하나를 무작위로 고름.")]
        [Min(1)]
        public int AttackRings = 3;

        [SerializeField]
        [Tooltip("지키기 진형 첫 고리 반지름")]
        [Min(0.0F)]
        public float SecureRadius = 3.0F;

        [SerializeField]
        [Tooltip("호위 진형 첫 고리와 모선 선체 바깥 사이 거리")]
        [Min(0.0F)]
        public float EscortRadius = 2.0F;

        [SerializeField]
        [Tooltip("고리 사이 거리")]
        [Min(0.1F)]
        public float RingGap = 3.0F;

        [SerializeField]
        [Tooltip("한 고리 위 자리 사이의 최소 거리")]
        [Min(0.1F)]
        public float SlotGap = 3.0F;

        [SerializeField]
        [Tooltip("공격 진형 자리가 고리를 따라 도는 속력")]
        [Min(0.0F)]
        public float OrbitSpeed = 4.0F;

        [SerializeField]
        [Tooltip("공격 진형 자리가 도는 방향을 바꾸는 간격(초) (최소, 최대)")]
        public Vector2 DodgeInterval = new Vector2(1.0F, 3.0F);

        [SerializeField]
        [Tooltip("공격 중 체력 비율이 이보다 낮으면 모선으로 돌아가 흡수됨.")]
        [Range(0.0F, 1.0F)]
        public float RetreatHealth = 0.3F;

        [Header("Turret")]

        [SerializeField]
        [Tooltip("대상 선체 바깥이 이 거리 안이어야 쏨. 적을 찾는 반지름과 별개.")]
        [Min(0.0F)]
        public float FireRange = 12.0F;

        [SerializeField]
        [Tooltip("출격 후 쏘지 않는 시간(초). 모선 안에서 쏘지 않게 함.")]
        [Min(0.0F)]
        public float LaunchWindUp = 1.0F;

        [SerializeField]
        [Tooltip("한 번에 사격하는 시간(초)")]
        [Min(0.0F)]
        public float FireDuration = 2.0F;

        [SerializeField]
        [Tooltip("사격 후 휴식 시간 범위(초) (최소, 최대)")]
        public Vector2 RestDurationRange = new Vector2(1.0F, 2.0F);

        [SerializeField]
        [Tooltip("사격을 허용하는 조준 오차(도)")]
        [Range(0.0F, 180.0F)]
        public float AimTolerance = 15.0F;
    }

    public sealed class CarriedCraftBaker : Baker<CarriedCraftAuthoring>
    {
        public override void Bake(CarriedCraftAuthoring authoring)
        {
            Entity entity = GetEntity(TransformUsageFlags.Dynamic);
            SpaceshipAuthoring spaceship = GetComponent<SpaceshipAuthoring>();

            AddComponent(entity, new CarriedCraftSpecComponent
            {
                Speed          = spaceship.Speed,
                Acceleration   = spaceship.Acceleration,
                Deceleration   = spaceship.Deceleration,
                HoverDistance  = authoring.HoverDistance,
                FlightDistance = math.max(authoring.FlightDistance, authoring.HoverDistance),
                LeashRadius    = authoring.LeashRadius,
                FireRange      = authoring.FireRange,
                LaunchWindUp   = authoring.LaunchWindUp,
                EngageDistance = authoring.EngageDistance,
                AttackRadius   = authoring.AttackRadius,
                AttackRings    = Mathf.Max(authoring.AttackRings, 1),
                SecureRadius   = authoring.SecureRadius,
                EscortRadius   = authoring.EscortRadius,
                RingGap        = authoring.RingGap,
                SlotGap        = authoring.SlotGap,
                OrbitSpeed     = authoring.OrbitSpeed,
                DodgeInterval  = new float2(
                    math.min(authoring.DodgeInterval.x, authoring.DodgeInterval.y),
                    math.max(authoring.DodgeInterval.x, authoring.DodgeInterval.y)
                ),
                RetreatHealth  = authoring.RetreatHealth,
            });

            // 모선이 출격시킬 때 채움.
            AddComponent(entity, new CarriedCraftComponent { SlotIndex = -1, Ring = -1 });

            // 격추한 플레이어의 점수용 막타. 서버만 채움.
            AddComponent<KillingBlowComponent>(entity);

            // 주운 재료. 서버만 채움.
            AddBuffer<CarriedItemElement>(entity);

            // 대상 고르기, 사격, 충돌 반응은 AI 우주선과 같은 컴포넌트를 씀.
            AddComponent(entity, new EnemyPerceptionComponent
            {
                CurrentTarget   = Entity.Null,
                Sight           = new ColliderComponent
                {
                    Shape   = ColliderShape.Circle,
                    Center  = float2.zero,
                    Extents = new float2(authoring.DetectRadius, 0.0F),
                },
                ChaseDistance   = authoring.DetectRadius,

                // 같은 편(파티)은 판정에서 빠짐.
                HostileFactions = (uint)(FactionID.Player | FactionID.Neutralize),
            });

            AddBuffer<SightOverlapElement>(entity);

            AddComponent(entity, new EnemySpaceshipMovementComponent
            {
                HoveringSpeed        = spaceship.HoveringSpeed,
                HoveringAcceleration = spaceship.HoveringAcceleration,
                HoveringDeceleration = spaceship.HoveringDeceleration,
                Maneuver             = spaceship.Maneuver,
                Velocity             = float2.zero,
            });

            AddComponent(entity, new EnemyTurretComponent
            {
                FireDuration      = authoring.FireDuration,
                RestDurationRange = new float2(
                    math.min(authoring.RestDurationRange.x, authoring.RestDurationRange.y),
                    math.max(authoring.RestDurationRange.x, authoring.RestDurationRange.y)
                ),
                AimCosine         = math.cos(math.radians(authoring.AimTolerance)),
                FireRange         = authoring.FireRange,
                CycleRemaining    = authoring.FireDuration,
                Resting           = false,
            });
        }
    }
}
