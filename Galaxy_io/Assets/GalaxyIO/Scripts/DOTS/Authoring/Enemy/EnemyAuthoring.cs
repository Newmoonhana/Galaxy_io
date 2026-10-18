using GalaxyIO.DOTS.Components.Actor;
using System.Collections.Generic;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

namespace GalaxyIO.DOTS.Authoring
{
    /// <summary>
    /// AI 행동 모듈 종류.
    /// </summary>
    public enum EnemyAIType : byte
    {
        Strafing = 0,
        Dogfight = 1,
        Floating = 2,
    }

    /// <summary>
    /// AI 가 조종하는 우주선의 행동 정의. 기체 스펙은 <see cref="SpaceshipAuthoring"/>을 따름.
    /// </summary>
    [RequireComponent(typeof(SpaceshipAuthoring))]
    public sealed class EnemyAuthoring : MonoBehaviour
    {
        [Header("Display")]

        [SerializeField]
        [Tooltip("킬 로그에 표시할 이름")]
        public string DisplayName = "";

        [Header("AI")]

        [SerializeField]
        [Tooltip("행동 모듈")]
        public EnemyAIType AIType = EnemyAIType.Strafing;

        [Header("Perception")]

        [SerializeField]
        [Tooltip("오브젝트 기준 시야 꼭짓점 오프셋")]
        public Vector2 SightCenter = Vector2.zero;

        [SerializeField]
        [Tooltip("시야 반지름")]
        [Min(0.0F)]
        public float SightRadius = 12.0F;

        [SerializeField]
        [Tooltip("시야각(도). 기수(+Y) 기준 좌우 대칭.")]
        [Range(0.0F, 360.0F)]
        public float SightAngle = 120.0F;

        [SerializeField]
        [Tooltip("대상을 놓치는 거리. 대상 선체 바깥(Thickness 절반)부터 잼. 시야 반지름보다 작으면 시야 반지름을 씀.")]
        [Min(0.0F)]
        public float ChaseDistance = 20.0F;

        [SerializeField]
        [Tooltip("시야에서 새 대상으로 삼을 진영. 피격 시에는 진영과 무관하게 공격자를 대상으로 삼음.")]
        public List<FactionID> HostileFactions = new List<FactionID> { FactionID.Player };

        [Header("Turret")]

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

        [Header("Strafing")]

        [SerializeField]
        [Tooltip("Idle 이동속도")]
        [Min(0.0F)]
        public float IdleSpeed = 2.0F;

        [SerializeField]
        [Tooltip("한 번에 좌/우로 이동할 거리 범위 (최소, 최대)")]
        public Vector2 StrafeDistanceRange = new Vector2(3.0F, 6.0F);

        [SerializeField]
        [Tooltip("이보다 멀면 좌/우 이동을 멈추고 추격. 대상 선체 바깥(Thickness 절반)부터 잼.")]
        [Min(0.0F)]
        public float ChaseRadius = 12.0F;
    }

    public sealed class EnemyBaker : Baker<EnemyAuthoring>
    {
        public override void Bake(EnemyAuthoring authoring)
        {
            Entity entity = GetEntity(TransformUsageFlags.Dynamic);
            SpaceshipAuthoring spaceship = GetComponent<SpaceshipAuthoring>();

            var displayName = new FixedString32Bytes();
            displayName.CopyFromTruncated(authoring.DisplayName);

            AddComponent(entity, new EnemyNameComponent { Value = displayName });

            // 점수용 막타. 서버만 채움.
            AddComponent<KillingBlowComponent>(entity);

            // 드랍 조건(플레이어 막타/어시스트) 판정용 피격 기록. 서버만 채움.
            AddBuffer<DamageRecordElement>(entity);

            AddComponent(entity, new EnemyPerceptionComponent
            {
                CurrentTarget  = Entity.Null,
                TargetPosition = float2.zero,
                Sight          = new ColliderComponent
                {
                    Shape   = ColliderShape.Sector,
                    Center  = new float2(authoring.SightCenter.x, authoring.SightCenter.y),
                    Extents = new float2(authoring.SightRadius, math.radians(authoring.SightAngle * 0.5F)),
                },
                // 시야보다 짧으면 찾자마자 놓침.
                ChaseDistance  = math.max(authoring.ChaseDistance, authoring.SightRadius),
                HostileFactions = ToMask(authoring.HostileFactions),
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
                RestDurationRange = SortRange(authoring.RestDurationRange),
                AimCosine         = math.cos(math.radians(authoring.AimTolerance)),
                // 적은 거리 제한 없이 대상을 정하면 쏨.
                FireRange         = float.MaxValue,
                CycleRemaining    = authoring.FireDuration,
                Resting           = false,
            });

            switch (authoring.AIType)
            {
                case EnemyAIType.Strafing:
                    AddComponent(entity, new StrafingBehaviorComponent
                    {
                        State               = StrafingStateID.Idle,
                        IdleSpeed           = authoring.IdleSpeed,
                        StrafeDistanceRange = SortRange(authoring.StrafeDistanceRange),
                        ChaseRadius         = authoring.ChaseRadius,
                        StrafeDirection     = 1.0F,
                        StrafeRemaining     = 0.0F,
                        WanderDirection     = float2.zero,
                    });

                    // 이벤트를 소비하는 행동 모듈에만 붙임. 소비자가 없으면 버퍼가 계속 쌓임.
                    AddBuffer<EnemyEventElement>(entity);
                    break;

                default:
                    Debug.LogWarning($"EnemyAuthoring: 구현되지 않은 AI 타입. ({authoring.AIType})", authoring);
                    break;
            }
        }

        // (최소, 최대) 순서로 맞춤.
        private static float2 SortRange(Vector2 range)
        {
            return new float2(math.min(range.x, range.y), math.max(range.x, range.y));
        }

        // 진영 목록을 비트 마스크로 합침. 중복은 무시됨.
        private static uint ToMask(List<FactionID> factions)
        {
            uint mask = 0;

            foreach (FactionID faction in factions)
            {
                mask |= (uint)faction;
            }

            return mask;
        }
    }
}