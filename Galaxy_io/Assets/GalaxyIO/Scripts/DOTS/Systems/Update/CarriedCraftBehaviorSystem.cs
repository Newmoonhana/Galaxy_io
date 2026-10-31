using GalaxyIO.DOTS.Collision;
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
    /// 함재기를 조종하는 시스템. <see cref="CarrierSystem"/>이 채운 진형 자리로 날아감.
    /// 교전 중이면 대상에게 곧장 다가가며 쏘다가, 자기 고리에 닿으면 고리를 따라 돎. 선체를 가로지르는 길은 고리를 따라 돌아감.
    /// 자리에서 멀면 비행 모드, 가까우면 호버링 모드로 움직이고, 비행 중에는 앞길의 적 선체를 좌우로 비켜 감.
    /// 고리 위에서 자리를 옮길 때는 대상을 바라본 채 비행 속력으로 미끄러지며 쏨.
    /// 사격은 <see cref="EnemyTurretFireSystem"/>이 함. 출격 직후 잠깐은 쏘지 않음.
    /// </summary>
    [BurstCompile]
    [WorldSystemFilter(WorldSystemFilterFlags.ServerSimulation)]
    [UpdateInGroup(typeof(AISystemGroup))]
    [UpdateAfter(typeof(EnemyPerceptionSystem))]
    public partial struct CarriedCraftBehaviorSystem : ISystem
    {
        private EntityQuery hullQuery;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            // 길을 막을 수 있는 선체. 함재기끼리는 진형을 짜므로 빼고, 아군 선체는 넣음.
            hullQuery = SystemAPI.QueryBuilder()
                .WithAll<SpaceshipSpecComponent, ColliderComponent, LocalTransform>()
                .WithNone<CarriedCraftComponent>()
                .Build();

            state.RequireForUpdate(SystemAPI.QueryBuilder().WithAll<CarriedCraftComponent>().Build());
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            state.CompleteDependency();

            NativeArray<PathObstacle> obstacles = PathAvoidance.Collect(
                hullQuery,
                SystemAPI.GetComponentLookup<PlayerSpaceshipMovementComponent>(true),
                Allocator.TempJob
            );

            state.Dependency = new CarriedCraftBehaviorJob
            {
                DeltaTime = SystemAPI.Time.DeltaTime,
                Specs     = SystemAPI.GetComponentLookup<SpaceshipSpecComponent>(true),
                Colliders = SystemAPI.GetComponentLookup<ColliderComponent>(true),
                Kings     = SystemAPI.GetComponentLookup<KingCarrierComponent>(true),
                Obstacles = obstacles,
            }.ScheduleParallel(state.Dependency);

            obstacles.Dispose(state.Dependency);
        }
    }

    [BurstCompile]
    [WithAll(typeof(Simulate))]
    public partial struct CarriedCraftBehaviorJob : IJobEntity
    {
        // 호버링 중 자리로 붙는 빠르기(1/초). 남은 거리 x 이 값이 목표 속력.
        private const float HoverGain = 2.0F;

        // 최고 속력으로 다가갈 때 선체 바깥에서 고리까지의 거리에 더하는 비율. 빠를수록 일찍 진형을 짜러 흘러감.
        private const float MaxApproachDrift = 0.5F;

        // 고리를 따라 돌아갈 때 한 번에 앞서 잡는 각도(라디안). 이만큼 떨어진 두 고리 위 점을 이은 길은 선체에서 충분히 멂.
        private const float ArcStep = math.PI * 0.25F;

        // 앞길의 선체를 살필 때 제동 거리에 더하는 반응 시간(초)
        private const float AvoidReaction = 0.25F;

        public float DeltaTime;

        [ReadOnly]
        public ComponentLookup<SpaceshipSpecComponent> Specs;

        [ReadOnly]
        public ComponentLookup<ColliderComponent> Colliders;

        [ReadOnly]
        public ComponentLookup<KingCarrierComponent> Kings;

        // 함재기를 뺀 모든 선체. 적 선체만 담기는 시야(sights)와 달리 아군 선체도 피함.
        [ReadOnly]
        public NativeArray<PathObstacle> Obstacles;

        private void Execute(
            in LocalTransform transform,
            ref EnemyPerceptionComponent perception,
            in EnemySpaceshipMovementComponent hovering,
            ref MovementComponent movement,
            ref CarriedCraftComponent craft,
            in CarriedCraftSpecComponent spec,
            in SpaceshipSpecComponent self,
            in DynamicBuffer<SightOverlapElement> sights)
        {
            float2 position = transform.Position.xy;
            float2 forward = math.normalizesafe(SpaceshipHeading.FromRotation(transform.Rotation), new float2(0.0F, 1.0F));
            float ownRadius = self.Thickness * 0.5F;

            craft.WindUpRemaining = math.max(craft.WindUpRemaining - DeltaTime, 0.0F);

            // 교전 대상은 모선이 고름. 귀환 중이거나 출격 직후에는 쏘지 않음.
            Specs.TryGetComponent(craft.Target, out SpaceshipSpecComponent targetSpec);
            bool engaging = !craft.Returning && craft.Engaging && craft.Target != Entity.Null && Specs.HasComponent(craft.Target);

            perception.CurrentTarget = engaging && craft.WindUpRemaining <= 0.0F ? craft.Target : Entity.Null;
            perception.TargetPosition = craft.Anchor;
            perception.TargetThickness = targetSpec.Thickness;

            float2 destination;
            float2 destinationVelocity;
            float2 face;

            // 고리를 따라 도는 중이면 대상 쪽으로 이 반지름 안에 들어가지 않음.
            float ringClearance = 0.0F;

            if (craft.Returning)
            {
                destination = craft.Anchor;
                destinationVelocity = craft.AnchorVelocity;
                face = forward;
            }
            else if (engaging && craft.Approaching)
            {
                // 대상을 바라보며 곧장 다가가 자기 고리 위에서 멈춤.
                float ringRadius = math.distance(craft.Slot, craft.Anchor);

                destination = craft.Anchor + math.normalizesafe(position - craft.Anchor, -forward) * ringRadius;
                destinationVelocity = craft.AnchorVelocity;
                face = craft.Anchor - position;

                // 선체 바깥에서 고리까지의 거리만 늘림. 큰 대상이라고 더 일찍 멈추지 않음.
                float closingSpeed = math.max(math.dot(movement.Velocity - craft.AnchorVelocity, math.normalizesafe(craft.Anchor - position)), 0.0F);
                float drift = 1.0F + MaxApproachDrift * math.saturate(closingSpeed / spec.Speed);
                float hull = craft.HullRadius + ownRadius;

                craft.Approaching = math.distance(position, craft.Anchor) > hull + (ringRadius - hull) * drift + spec.HoverDistance;
            }
            else if (engaging)
            {
                // 고리 위 자리로 감. 곧장 가는 길이 선체를 지나면 고리를 따라 돌아감.
                // 이미 고리를 따라 돌고 있으면 돌아서지 않게 그 방향으로 돎.
                float ringRadius = math.distance(craft.Slot, craft.Anchor);
                float clearance = craft.HullRadius + ownRadius + spec.HoverDistance;
                float2 fromCenter = position - craft.Anchor;
                float2 path = craft.Slot - position;
                float2 closest = fromCenter + path * math.saturate(-math.dot(fromCenter, path) / math.max(math.lengthsq(path), math.EPSILON));

                destination = craft.Slot;

                if (math.length(closest) < clearance)
                {
                    float current = CarrierFormation.Angle(fromCenter);
                    float slotAngle = CarrierFormation.Angle(craft.Slot - craft.Anchor);
                    float delta = math.atan2(math.sin(slotAngle - current), math.cos(slotAngle - current));
                    float tangential = math.dot(movement.Velocity - craft.AnchorVelocity, CarrierFormation.Direction(current + math.PI * 0.5F));
                    float turn = math.abs(tangential) > spec.HoverDistance ? math.sign(tangential) : math.sign(delta);
                    float arc = turn == math.sign(delta) ? math.abs(delta) : 2.0F * math.PI - math.abs(delta);

                    destination = craft.Anchor
                                  + CarrierFormation.Direction(current + turn * math.min(arc, ArcStep))
                                  * math.max(ringRadius, clearance);
                }

                destinationVelocity = craft.AnchorVelocity;
                face = craft.Anchor - position;
                ringClearance = ringRadius - spec.HoverDistance;
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
                // 자리에 멈출 수 있는 속력까지만 내고, 자리가 움직이면 그 속력을 더함.
                float2 direction = distance > math.EPSILON ? toDestination / distance : forward;

                // 비켜 갈 옆쪽은 지금 움직이는 쪽을 따름.
                float2 moving = math.normalizesafe(movement.Velocity, forward);

                // 대장선 함재기는 플레이어 우주선과 부딪히지 않으므로 피하지 않음.
                bool kingCraft = Kings.HasComponent(craft.Carrier);

                // 멈출 수 있는 거리 안, 자리에 닿기 전에 선체(시야의 적 선체 + 아군을 포함한 모든 선체)가 있으면 가장 먼저 닿을 선체의 바깥쪽 접선으로 좌우로 비켜 가고
                // 그 선체 앞에서 멈출 수 있는 속력까지 줄임. 교전 대상은 그 속도를 빼고 봄.
                // 이미 안쪽이면 돌아서지 않고 가던 쪽 옆으로 빠져나감.
                float nearestEntry = float.MaxValue;
                float speedLimit = float.MaxValue;
                float2 avoid = direction;
                bool escaping = false;

                for (int i = 0; i < sights.Length + Obstacles.Length && !escaping; i++)
                {
                    Entity other;
                    float2 otherCenter;

                    if (i < sights.Length)
                    {
                        other = sights[i].Message.Other;
                        otherCenter = sights[i].Message.OtherCenter;
                    }
                    else
                    {
                        PathObstacle path = Obstacles[i - sights.Length];

                        // 자기 모선은 통과함. 교전 대상은 시야 쪽에서 고리를 따라 돌게 함. 아주 작은 선체는 무시함.
                        if (path.Entity == craft.Carrier
                            || path.Entity == craft.Target
                            || (kingCraft && path.Player)
                            || PathAvoidance.IsNegligible(path.Thickness, self.Thickness))
                        {
                            continue;
                        }

                        other = path.Entity;
                        otherCenter = path.Center;
                    }

                    if (!Colliders.TryGetComponent(other, out ColliderComponent obstacle))
                    {
                        continue;
                    }

                    float2 toObstacle = otherCenter - position;
                    float obstacleDistance = math.length(toObstacle);
                    float2 toCenter = toObstacle / math.max(obstacleDistance, math.EPSILON);
                    float clearance = CarrierFormation.HullRadius(new ColliderWorld { Shape = obstacle.Shape, Extents = obstacle.Extents })
                                      + ownRadius + spec.HoverDistance;

                    // 자리가 이 선체 여유 안에 있으면 피해서는 닿을 수 없음. 시야 밖 선체(아군 등)는 이때 피하지 않음.
                    if (i >= sights.Length && math.distancesq(destination, otherCenter) <= clearance * clearance)
                    {
                        continue;
                    }

                    // 교전 대상은 고리 안쪽까지 피해서 고리를 따라 돌게 함. 고리 안쪽에 들어왔으면 옆으로 빠지되 선체는 계속 피함.
                    if (other == craft.Target && obstacleDistance <= ringClearance && obstacleDistance > clearance)
                    {
                        float2 sideways = new float2(-toCenter.y, toCenter.x);

                        avoid = math.dot(sideways, moving) >= 0.0F ? sideways : -sideways;
                    }
                    else if (other == craft.Target)
                    {
                        clearance = math.max(clearance, ringClearance);
                    }

                    if (obstacleDistance <= clearance)
                    {
                        float2 across = new float2(-toCenter.y, toCenter.x);

                        avoid = math.dot(across, moving) >= 0.0F ? across : -across;
                        speedLimit = float.MaxValue;
                        escaping = true;
                        continue;
                    }

                    float2 obstacleVelocity = other == craft.Target ? craft.AnchorVelocity : float2.zero;
                    float2 relative = movement.Velocity - obstacleVelocity;
                    float relativeSpeed = math.length(relative);
                    float2 heading = relativeSpeed > math.EPSILON ? relative / relativeSpeed : direction;
                    float reach = math.min(
                        distance,
                        relativeSpeed * relativeSpeed / (2.0F * spec.Deceleration) + relativeSpeed * AvoidReaction + ownRadius
                    );
                    float along = math.dot(toObstacle, heading);
                    float2 side = toObstacle - heading * along;
                    float miss = math.length(side);

                    if (along <= 0.0F || miss >= clearance)
                    {
                        continue;
                    }

                    float entry = along - math.sqrt(clearance * clearance - miss * miss);

                    if (entry > reach || entry >= nearestEntry)
                    {
                        continue;
                    }

                    // 선체 중심에서 비껴 있는 쪽으로 접선을 그음.
                    float2 outward = miss > math.EPSILON ? -side / miss : new float2(-heading.y, heading.x);
                    float sin = clearance / obstacleDistance;

                    nearestEntry = entry;
                    avoid = toCenter * math.sqrt(1.0F - sin * sin) + outward * sin;
                    speedLimit = math.sqrt(2.0F * spec.Deceleration * math.max(entry, 0.0F)) + math.max(math.dot(obstacleVelocity, avoid), 0.0F);
                }

                direction = avoid;

                float speed = math.min(
                    spec.Speed,
                    math.sqrt(2.0F * spec.Deceleration * distance) + math.max(math.dot(destinationVelocity, direction), 0.0F)
                );

                speed = math.min(speed, speedLimit);

                // 고리 위에서 자리를 옮길 때는 대상을 바라본 채 미끄러짐. 그 밖에는 기수 방향으로 날아감.
                bool drifting = engaging && !craft.Approaching;

                nextForward = SpaceshipHeading.RotateTowards(forward, drifting ? math.normalizesafe(face, forward) : direction, maxTurn);
                nextVelocity = SpaceshipHovering.Approach(
                    movement.Velocity,
                    (drifting ? direction : nextForward) * speed,
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
                    movement.Velocity,
                    destinationVelocity + approach,
                    hovering.HoveringAcceleration,
                    hovering.HoveringDeceleration,
                    hovering.HoveringAcceleration,
                    ref turning,
                    DeltaTime
                );
            }

            movement.Velocity = nextVelocity;
            movement.AngularVelocity = SpaceshipHeading.AngularVelocity(forward, nextForward, DeltaTime);
        }
    }
}