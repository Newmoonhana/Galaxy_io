using GalaxyIO.DOTS.Components.Actor;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

namespace GalaxyIO.DOTS.Authoring
{
    /// <summary>
    /// 이 오브젝트가 "플레이어에게 조종되고 있음"을 나타내는 컴포넌트.
    /// </summary>
    public sealed class PlayerPossessingAuthoring : MonoBehaviour
    {
    }

    public sealed class PlayerPossessingBaker : Baker<PlayerPossessingAuthoring>
    {
        public override void Bake(PlayerPossessingAuthoring authoring)
        {
            Entity entity = GetEntity(TransformUsageFlags.Dynamic);
            AddComponent<PlayerInputComponent>(entity);

            // 값은 스폰 시 서버가 채움.
            AddComponent<PlayerNameComponent>(entity);

            // 킬 로그용 피격 기록과 막타. 서버만 채움.
            AddBuffer<DamageRecordElement>(entity);
            AddComponent<KillingBlowComponent>(entity);

            SpaceshipAuthoring authoringShip = GetComponent<SpaceshipAuthoring>();
            SpaceshipSpecAsset spaceship = authoringShip != null ? authoringShip.Spec : null;

            DependsOn(spaceship);

            // 우주선이 아닌 오브젝트에도 붙을 수 있으므로 스펙이 없으면 비행 모델은 생략함.
            if (spaceship == null)
            {
                return;
            }

            AddComponent(entity, new PlayerSpaceshipMovementComponent
            {
                MaxSpeed             = spaceship.Speed,
                HoveringSpeed        = spaceship.HoveringSpeed,
                HoveringAcceleration = spaceship.HoveringAcceleration,
                HoveringDeceleration = spaceship.HoveringDeceleration,
                Velocity             = float2.zero,
                Acceleration         = spaceship.Acceleration,
                Deceleration         = spaceship.Deceleration,
                Maneuver             = spaceship.Maneuver,
                ManeuverAcceleration = spaceship.ManeuverAcceleration,
                ManeuverDeceleration = spaceship.ManeuverDeceleration,
                ManeuverPenalty      = spaceship.ManeuverPenalty,
                HoveringManeuver     = spaceship.HoveringManeuver,
                PhaseSpeed           = spaceship.PhaseSpeed,
            });

            AddComponent(entity, new TurnBrakeComponent
            {
                TurnRate     = spaceship.TurnBrakeTurnRate,
                Deceleration = spaceship.TurnBrakeDeceleration,
                Exponent     = spaceship.TurnBrakeExponent,
                StartSpeed   = spaceship.TurnBrakeStartSpeed,

                // StartSpeed 와 같거나 작으면 나눌 수 없음.
                ReferenceSpeed = Mathf.Max(spaceship.TurnBrakeReferenceSpeed, spaceship.TurnBrakeStartSpeed + 0.01F),

                Inertia             = spaceship.Inertia,
                InertiaDeceleration = spaceship.InertiaDeceleration,
                InertiaTurnRate     = spaceship.InertiaTurnRate,
            });

            AddComponent(entity, new SpaceshipCameraComponent
            {
                MaxOrthographicSize = spaceship.MaxOrthographicSize,
            });

            AddComponent(entity, new TurretHeatComponent
            {
                MaxHeat = spaceship.MaxHeat,
            });
        }
    }
}