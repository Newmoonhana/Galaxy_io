using GalaxyIO.DOTS.Components.Actor;
using GalaxyIO.DOTS.Components.Singleton;
using GalaxyIO.DOTS.Spaceship;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.NetCode;
using Unity.Transforms;
using Random = Unity.Mathematics.Random;

namespace GalaxyIO.DOTS.Systems
{
    /// <summary>
    /// Strafing AI 의 이벤트 처리, 상태 전이, 조종을 담당하는 시스템.
    /// 기수 회전과 <see cref="SpaceshipMovementComponent"/>를 직접 씀.
    /// </summary>
    [BurstCompile]
    [WorldSystemFilter(WorldSystemFilterFlags.ServerSimulation)]
    [UpdateInGroup(typeof(AISystemGroup))]
    [UpdateAfter(typeof(EnemyPerceptionSystem))]
    public partial struct StrafingBehaviorSystem : ISystem
    {
        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<NetworkTime>();
            state.RequireForUpdate<ItemRegistryElement>();
            state.RequireForUpdate(SystemAPI.QueryBuilder().WithAll<StrafingBehaviorComponent>().Build());
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            state.Dependency = new StrafingBehaviorJob
            {
                DeltaTime = SystemAPI.Time.DeltaTime,
                Tick      = SystemAPI.GetSingleton<NetworkTime>().ServerTick.SerializedData,
                Registry  = SystemAPI.GetSingletonBuffer<ItemRegistryElement>(true),
            }.ScheduleParallel(state.Dependency);
        }
    }

    /// <summary>
    /// 상태 전이 판단에 쓰는 이번 틱의 조건.
    /// </summary>
    public struct StrafingSignals
    {
        // 인지 시스템 또는 이벤트가 지정한 대상이 있음
        public bool HasTarget;

        // 이번 틱에 피격 이벤트에 반응함
        public bool Attacked;
    }

    [BurstCompile]
    [WithAll(typeof(Simulate))]
    public partial struct StrafingBehaviorJob : IJobEntity
    {
        public float DeltaTime;
        public uint Tick;

        [ReadOnly]
        public DynamicBuffer<ItemRegistryElement> Registry;

        private void Execute(
            Entity entity,
            ref LocalTransform transform,
            ref EnemyPerceptionComponent perception,
            ref StrafingBehaviorComponent strafing,
            ref EnemySpaceshipMovementComponent enemyMovement,
            ref SpaceshipMovementComponent movement,
            in TurretSwitchComponent turretSwitch,
            in EquippedTurretComponent equipped,
            DynamicBuffer<EnemyEventElement> events)
        {
            float2 position = transform.Position.xy;
            float2 forward = math.normalizesafe(
                SpaceshipHeading.FromRotation(transform.Rotation),
                new float2(1.0F, 0.0F)
            );

            // 엔티티와 틱을 섞어 같은 틱의 적끼리도 값이 갈림.
            var random = Random.CreateFromIndex(math.hash(new uint2((uint)entity.Index, Tick)) & 0x7FFFFFFFu);

            bool attacked = React(position, events, ref perception);
            events.Clear();

            StrafingStateID next = NextState(
                strafing.State,
                new StrafingSignals
                {
                    HasTarget = perception.CurrentTarget != Entity.Null,
                    Attacked  = attacked,
                }
            );

            if (next != strafing.State)
            {
                Enter(next, ref strafing, ref random);
            }

            float2 desiredForward;
            float2 desiredVelocity;

            switch (strafing.State)
            {
                case StrafingStateID.Idle:
                    TickIdle(forward, ref strafing, out desiredForward, out desiredVelocity);
                    break;

                case StrafingStateID.Engaging:
                    TickEngaging(
                        position,
                        perception.TargetPosition,
                        perception.TargetThickness,
                        forward,
                        enemyMovement.HoveringSpeed,
                        math.length(enemyMovement.Velocity) * DeltaTime,
                        ref strafing,
                        ref random,
                        out desiredForward,
                        out desiredVelocity
                    );
                    break;

                default:
                    desiredForward = forward;
                    desiredVelocity = float2.zero;
                    break;
            }

            // 가감속/회전 한계는 플레이어와 같은 헬퍼를 씀. 사격 중이면 장착 포탑에 따라 회전이 더 둔해짐.
            float2 nextForward = SpaceshipHeading.RotateTowards(
                forward,
                math.normalizesafe(desiredForward, forward),
                math.radians(enemyMovement.Maneuver)
                * TurretSwitch.ManeuverScale(turretSwitch, Registry.Get(equipped.Value))
                * DeltaTime
            );

            bool turning = false;

            float2 nextVelocity = SpaceshipHovering.Approach(
                enemyMovement.Velocity,
                desiredVelocity,
                enemyMovement.HoveringAcceleration,
                enemyMovement.HoveringDeceleration,
                // 적은 방향 전환도 가속도 그대로.
                enemyMovement.HoveringAcceleration,
                ref turning,
                DeltaTime
            );

            enemyMovement.Velocity = nextVelocity;
            transform.Rotation = SpaceshipHeading.ToRotation(nextForward);
            movement.DeltaMovement = nextVelocity;
        }

        /// <summary>
        /// 외부 이벤트의 효과를 적용함. 전이 조건에 반영할 반응이 있었으면 true.
        /// </summary>
        private static bool React(
            float2 position,
            DynamicBuffer<EnemyEventElement> events,
            ref EnemyPerceptionComponent perception)
        {
            bool attacked = false;

            for (int i = 0; i < events.Length; i++)
            {
                EnemyEventElement e = events[i];

                switch (e.ID)
                {
                    case EnemyEventID.ProjectileHit:
                        // 추격 거리 밖의 공격자는 무시. 지정해도 다음 틱 인지 시스템이 놓침.
                        if (math.distancesq(position, e.InstigatorPosition)
                            > perception.ChaseDistance * perception.ChaseDistance)
                        {
                            break;
                        }

                        attacked = true;

                        // 교전 중인 대상은 바꾸지 않음. 시야 밖이어도 공격자를 대상으로 삼음.
                        if (perception.CurrentTarget == Entity.Null)
                        {
                            perception.CurrentTarget = e.Instigator;
                            perception.TargetPosition = e.InstigatorPosition;

                            // 다음 틱 인지 시스템이 채움.
                            perception.TargetThickness = 0.0F;
                        }
                        break;
                }
            }

            return attacked;
        }

        /// <summary>
        /// 전이 표. 상태별로 갈 수 있는 다음 상태만 적음.
        /// </summary>
        private static StrafingStateID NextState(StrafingStateID current, in StrafingSignals signals)
        {
            switch (current)
            {
                case StrafingStateID.Idle:
                    return signals.HasTarget || signals.Attacked ? StrafingStateID.Engaging : current;

                case StrafingStateID.Engaging:
                    return signals.HasTarget ? current : StrafingStateID.Idle;

                default:
                    return current;
            }
        }

        /// <summary>
        /// 상태 진입 시 한 번 실행.
        /// </summary>
        private static void Enter(StrafingStateID next, ref StrafingBehaviorComponent strafing, ref Random random)
        {
            switch (next)
            {
                case StrafingStateID.Idle:
                    strafing.WanderDirection = random.NextFloat2Direction();
                    break;

                case StrafingStateID.Engaging:
                    PickStrafe(ref strafing, ref random);
                    break;
            }

            strafing.State = next;
        }

        /// <summary>
        /// 배회 방향으로 기수를 돌리며 바라보는 방향으로 느리게 전진.
        /// </summary>
        private static void TickIdle(
            float2 forward,
            ref StrafingBehaviorComponent strafing,
            out float2 desiredForward,
            out float2 desiredVelocity)
        {
            // 스폰 직후엔 배회 방향이 없음. 바라보던 방향을 그대로 씀.
            if (math.lengthsq(strafing.WanderDirection) < math.EPSILON)
            {
                strafing.WanderDirection = forward;
            }

            desiredForward = strafing.WanderDirection;
            desiredVelocity = forward * strafing.IdleSpeed;
        }

        /// <summary>
        /// 대상을 바라보며 좌/우로 이동. 너무 멀면 추격. 거리는 대상 선체 바깥부터 잼.
        /// </summary>
        private static void TickEngaging(
            float2 position,
            float2 targetPosition,
            float targetThickness,
            float2 forward,
            float hoveringSpeed,
            float travelled,
            ref StrafingBehaviorComponent strafing,
            ref Random random,
            out float2 desiredForward,
            out float2 desiredVelocity)
        {
            float2 toTarget = targetPosition - position;
            float distance = math.length(toTarget);
            float2 radial = distance > math.EPSILON ? toTarget / distance : forward;

            // 이동 방향과 무관하게 항상 대상을 바라봄.
            desiredForward = radial;

            if (distance > strafing.ChaseRadius + targetThickness * 0.5F)
            {
                desiredVelocity = radial * hoveringSpeed;
                return;
            }

            // 뽑은 거리만큼 이동하면 방향과 거리를 다시 뽑음.
            strafing.StrafeRemaining -= travelled;

            if (strafing.StrafeRemaining <= 0.0F)
            {
                PickStrafe(ref strafing, ref random);
            }

            float2 left = new float2(-radial.y, radial.x);
            desiredVelocity = left * (strafing.StrafeDirection * hoveringSpeed);
        }

        private static void PickStrafe(ref StrafingBehaviorComponent strafing, ref Random random)
        {
            strafing.StrafeDirection = random.NextBool() ? 1.0F : -1.0F;
            strafing.StrafeRemaining = random.NextFloat(strafing.StrafeDistanceRange.x, strafing.StrafeDistanceRange.y);
        }
    }
}