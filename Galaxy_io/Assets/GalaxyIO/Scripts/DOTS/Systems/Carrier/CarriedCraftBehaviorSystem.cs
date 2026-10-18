using GalaxyIO.DOTS.Components.Actor;
using GalaxyIO.DOTS.Spaceship;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace GalaxyIO.DOTS.Systems
{
    /// <summary>
    /// 함재기를 조종하는 시스템. <see cref="CarrierSystem"/>이 채운 진형 자리로 날아가고, 적을 찾으면 진형을 벗어나 교전함.
    /// 자리에서 멀면 비행 모드, 가까우면 호버링 모드로 움직임. 사격은 <see cref="EnemyTurretFireSystem"/>이 함.
    /// 공격 명령은 대상에게 곧장 다가가며 쏘다가, 자기 고리에 닿으면 진형을 짜고 돎.
    /// 출격 직후 잠깐은 쏘지도, 교전하지도 않음.
    /// </summary>
    [BurstCompile]
    [WorldSystemFilter(WorldSystemFilterFlags.ServerSimulation)]
    [UpdateInGroup(typeof(AISystemGroup))]
    [UpdateAfter(typeof(EnemyPerceptionSystem))]
    public partial struct CarriedCraftBehaviorSystem : ISystem
    {
        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate(SystemAPI.QueryBuilder().WithAll<CarriedCraftComponent>().Build());
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            state.Dependency = new CarriedCraftBehaviorJob
            {
                DeltaTime = SystemAPI.Time.DeltaTime,
                Specs     = SystemAPI.GetComponentLookup<SpaceshipSpecComponent>(true),
            }.ScheduleParallel(state.Dependency);
        }
    }

    [BurstCompile]
    [WithAll(typeof(Simulate))]
    public partial struct CarriedCraftBehaviorJob : IJobEntity
    {
        // 호버링 중 자리로 붙는 빠르기(1/초). 남은 거리 x 이 값이 목표 속력.
        private const float HoverGain = 2.0F;

        public float DeltaTime;

        [ReadOnly]
        public ComponentLookup<SpaceshipSpecComponent> Specs;

        private void Execute(
            ref LocalTransform transform,
            ref EnemyPerceptionComponent perception,
            ref EnemySpaceshipMovementComponent hovering,
            ref SpaceshipMovementComponent movement,
            ref CarriedCraftComponent craft,
            in CarriedCraftSpecComponent spec)
        {
            float2 position = transform.Position.xy;
            float2 forward = math.normalizesafe(SpaceshipHeading.FromRotation(transform.Rotation), new float2(0.0F, 1.0F));

            craft.WindUpRemaining = math.max(craft.WindUpRemaining - DeltaTime, 0.0F);

            // 귀환 중이거나 출격 직후에는 쏘지 않음. 공격 명령은 지정한 대상만 노림.
            // 그 밖에는 인지 시스템이 고른 대상을 쓰되, 호위 중에는 모선에서 너무 먼 대상을 놓음.
            if (craft.Returning || craft.WindUpRemaining > 0.0F)
            {
                perception.CurrentTarget = Entity.Null;
            }
            else if (craft.Order == CarrierOrder.Attack)
            {
                bool alive = Specs.TryGetComponent(craft.Target, out SpaceshipSpecComponent targetSpec);

                perception.CurrentTarget = alive ? craft.Target : Entity.Null;
                perception.TargetPosition = craft.Anchor;
                perception.TargetThickness = alive ? targetSpec.Thickness : 0.0F;
            }
            else if (perception.CurrentTarget != Entity.Null
                     && craft.Order == CarrierOrder.Escort
                     && math.distancesq(perception.TargetPosition, craft.Anchor) > spec.LeashRadius * spec.LeashRadius)
            {
                perception.CurrentTarget = Entity.Null;
            }

            bool engaging = !craft.Returning && craft.Order != CarrierOrder.Attack && perception.CurrentTarget != Entity.Null;

            float2 destination;
            float2 destinationVelocity;
            float2 face;

            if (craft.Returning)
            {
                destination = craft.Anchor;
                destinationVelocity = craft.AnchorVelocity;
                face = forward;
            }
            else if (engaging)
            {
                // 진형을 벗어나 대상 선체 바깥에서 일정 거리를 두고 싸움.
                float2 away = math.normalizesafe(position - perception.TargetPosition, -forward);

                destination = perception.TargetPosition + away * (spec.EngageDistance + perception.TargetThickness * 0.5F);
                destinationVelocity = float2.zero;
                face = perception.TargetPosition - position;
            }
            else if (craft.Order == CarrierOrder.Attack && craft.Approaching)
            {
                // 대상을 바라보며 곧장 다가가 자기 고리 위에서 멈춤.
                float ringRadius = math.distance(craft.Slot, craft.Anchor);

                destination = craft.Anchor + math.normalizesafe(position - craft.Anchor, -forward) * ringRadius;
                destinationVelocity = craft.AnchorVelocity;
                face = craft.Anchor - position;

                craft.Approaching = math.distance(position, craft.Anchor) > (ringRadius + spec.HoverDistance);
            }
            else
            {
                // 호위는 모선과 같은 쪽을, 나머지는 진형 중심을 바라봄.
                destination = craft.Slot;
                destinationVelocity = craft.AnchorVelocity;
                face = craft.Order == CarrierOrder.Escort ? craft.Heading : craft.Anchor - position;
            }

            float2 toDestination = destination - position;
            float distance = math.length(toDestination);

            if (craft.Returning)
            {
                craft.Flying = true;
            }
            else if (craft.Flying ? distance < spec.HoverDistance : distance > spec.FlightDistance)
            {
                craft.Flying = !craft.Flying;
            }

            float maxTurn = math.radians(hovering.Maneuver) * DeltaTime;
            bool turning = false;
            float2 nextForward;
            float2 nextVelocity;

            if (craft.Flying)
            {
                // 기수 방향으로 날아감. 자리에 멈출 수 있는 속력까지만 내고, 자리가 움직이면 그 속력을 더함.
                float2 direction = distance > math.EPSILON ? toDestination / distance : forward;
                float speed = math.min(
                    spec.Speed,
                    math.sqrt(2.0F * spec.Deceleration * distance) + math.max(math.dot(destinationVelocity, direction), 0.0F)
                );

                nextForward = SpaceshipHeading.RotateTowards(forward, direction, maxTurn);
                nextVelocity = SpaceshipHovering.Approach(
                    hovering.Velocity,
                    nextForward * speed,
                    spec.Acceleration,
                    spec.Deceleration,
                    spec.Acceleration,
                    ref turning,
                    DeltaTime
                );
            }
            else
            {
                // 자리를 따라가며 바라볼 쪽으로 기수를 돌림.
                float2 approach = toDestination * HoverGain;
                float approachSpeed = math.length(approach);

                if (approachSpeed > hovering.HoveringSpeed)
                {
                    approach *= hovering.HoveringSpeed / approachSpeed;
                }

                nextForward = SpaceshipHeading.RotateTowards(forward, math.normalizesafe(face, forward), maxTurn);
                nextVelocity = SpaceshipHovering.Approach(
                    hovering.Velocity,
                    destinationVelocity + approach,
                    hovering.HoveringAcceleration,
                    hovering.HoveringDeceleration,
                    hovering.HoveringAcceleration,
                    ref turning,
                    DeltaTime
                );
            }

            hovering.Velocity = nextVelocity;
            transform.Rotation = SpaceshipHeading.ToRotation(nextForward);
            movement.DeltaMovement = nextVelocity;
        }
    }
}