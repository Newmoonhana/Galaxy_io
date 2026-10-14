using GalaxyIO.DOTS.Components.Actor;
using GalaxyIO.DOTS.Components.Singleton;
using GalaxyIO.DOTS.Spaceship;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace GalaxyIO.DOTS.Systems
{
    /// <summary>
    /// <see cref="PlayerInputComponent"/>를 읽어들여 플레이어 Spaceship의 기수 회전과
    /// 이동 벡터를 만드는 시스템. 클라이언트에서 먼저 업데이트되고 서버가 나중에 검증함.
    /// <p/>
    /// 기수 방향은 마우스 커서, 전진 속력은 가속 키(비행 모드),
    /// 기수와 무관한 평행 이동은 WASD(호버링 모드)가 담당함.
    /// 두 모드 모두 속도 벡터 하나(<see cref="PlayerSpaceshipMovementComponent.Velocity"/>)를 고침.
    /// </summary>
    [UpdateInGroup(typeof(MovementSystemGroup))]
    public partial struct PlayerMovementSystem : ISystem
    {
        // 커서가 기체로부터 이 거리(월드 유닛) 안에 있으면 방향 제어 입력으로 치지 않음.
        private const float CursorDeadzone = 0.5F;

        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<PlayerInputComponent>();
            state.RequireForUpdate<ItemRegistryElement>();
            state.RequireForUpdate<TurnBrakeConfigComponent>();
        }

        // 플레이어 입력 > 우주선의 기수 회전, DeltaMovement 수정
        public void OnUpdate(ref SystemState state)
        {
            DynamicBuffer<ItemRegistryElement> registry = SystemAPI.GetSingletonBuffer<ItemRegistryElement>(true);
            TurnBrakeConfigComponent turnBrake = SystemAPI.GetSingleton<TurnBrakeConfigComponent>();

            foreach (
                var (transform, inputRequest, movement, playerMovement, turretSwitch, equipped) in
                SystemAPI
                    .Query<
                        RefRW<LocalTransform>,
                        RefRO<PlayerInputComponent>,
                        RefRW<SpaceshipMovementComponent>,
                        RefRW<PlayerSpaceshipMovementComponent>,
                        RefRO<TurretSwitchComponent>,
                        RefRO<EquippedTurretComponent>
                    >()
                    .WithAll<Simulate>()
            )
            {
                float deltaTime = SystemAPI.Time.DeltaTime;
                bool accelerating = inputRequest.ValueRO.AccelerationTriggered;

                // 기수 방향은 항상 마우스 커서가 잡음.
                float2 desiredDirection = inputRequest.ValueRO.MouseCursor - transform.ValueRO.Position.xy;

                // 커서가 기체에 겹치면 방향이 튀므로 방향 제어 입력 없음으로 취급.
                if (math.lengthsq(desiredDirection) < CursorDeadzone * CursorDeadzone)
                {
                    desiredDirection = float2.zero;
                }

                float2 velocity = playerMovement.ValueRO.Velocity;
                float speed = math.length(velocity);
                float maxSpeed = playerMovement.ValueRO.MaxSpeed;

                // 회전: 목표 방향까지 Maneuver 한계 내에서 접근.
                float2 currentForward = math.normalizesafe(
                    SpaceshipHeading.FromRotation(transform.ValueRO.Rotation),
                    new float2(1.0F, 0.0F)
                );

                float2 desiredForward = math.normalizesafe(desiredDirection, currentForward);

                // 급선회 방지. 속도가 빠를수록 Maneuver를 감쇄시킴.
                float speedRatio = maxSpeed > math.EPSILON ? math.saturate(speed / maxSpeed) : 0.0F;
                float maneuverScale = math.lerp(
                    1.0F,
                    1.0F - math.saturate(playerMovement.ValueRO.ManeuverPenalty),
                    speedRatio
                );

                // 사격 중이면 장착 포탑에 따라 회전이 둔해짐. 속력 감쇄와 겹치지 않고 더 큰 감소 하나만 적용함.
                maneuverScale = math.min(
                    maneuverScale,
                    TurretSwitch.ManeuverScale(turretSwitch.ValueRO, registry.Get(equipped.ValueRO.Value))
                );

                float maxAngleDelta = math.radians(playerMovement.ValueRO.Maneuver) * maneuverScale * deltaTime;
                float2 nextForward = SpaceshipHeading.RotateTowards(currentForward, desiredForward, maxAngleDelta);

                float turnAngle = math.abs(math.atan2(
                    currentForward.x * nextForward.y - currentForward.y * nextForward.x,
                    math.dot(currentForward, nextForward)
                ));

                float hoveringSpeed = playerMovement.ValueRO.HoveringSpeed;

                // 호버링: 가속 키를 떼고 호버링 속력 이하로 느려지면 기수와 무관하게
                // WASD 합산 방향으로 평행 이동함.
                if (!accelerating && speed <= hoveringSpeed)
                {
                    float2 desiredHovering =
                        math.normalizesafe(inputRequest.ValueRO.MoveArrows, float2.zero) * hoveringSpeed;

                    // 방향 전환 여부는 매 틱 현재 속도와 목표 방향을 비교해 정함.
                    bool turning = false;

                    velocity = SpaceshipHovering.Approach(
                        velocity,
                        desiredHovering,
                        playerMovement.ValueRO.HoveringAcceleration,
                        playerMovement.ValueRO.HoveringDeceleration,
                        playerMovement.ValueRO.HoveringManeuver,
                        ref turning,
                        deltaTime
                    );
                }
                else
                {
                    // 선회 제동: 이번 틱의 회전속도(도/초)가 빠를수록 감속도가 커짐.
                    // 속력이 StartSpeed 를 넘는 만큼 선형으로 더 커짐.
                    float turnDeceleration = turnBrake.Deceleration
                                             * math.pow(
                                                 math.degrees(turnAngle) / deltaTime / turnBrake.TurnRate,
                                                 turnBrake.Exponent
                                             )
                                             * math.max(speed - turnBrake.StartSpeed, 0.0F)
                                             / (turnBrake.ReferenceSpeed - turnBrake.StartSpeed);

                    // 회전 관성: 속도는 기수를 따라 돌지만, 선회 제동이 셀수록 덜 따라 돌아 돌기 전 방향으로 밀려남.
                    // 뒤처진 속도는 InertiaTurnRate 로 기수를 뒤늦게 따라 돎.
                    float grip = 1.0F - math.saturate(
                        turnBrake.Inertia * turnDeceleration / math.max(turnBrake.Deceleration, math.EPSILON)
                    );

                    if (speed > math.EPSILON)
                    {
                        velocity = SpaceshipHeading.RotateTowards(
                                       velocity / speed,
                                       nextForward,
                                       turnAngle * grip + math.radians(turnBrake.InertiaTurnRate) * deltaTime
                                   )
                                   * speed;
                    }

                    // 기수 방향 성분: 가속 키를 누르면 MaxSpeed로, 떼면 0으로 접근한 뒤 선회 제동만큼 깎임.
                    float along = math.dot(velocity, nextForward);
                    float2 side = velocity - nextForward * along;

                    float desiredSpeed = accelerating ? maxSpeed : 0.0F;
                    float maxSpeedDelta = deltaTime * (accelerating
                        ? playerMovement.ValueRO.Acceleration
                        : playerMovement.ValueRO.Deceleration);

                    along += math.clamp(desiredSpeed - along, -maxSpeedDelta, maxSpeedDelta);
                    along -= math.min(turnDeceleration * deltaTime, math.max(along, 0.0F));

                    // 옆 성분(밀려나는 속도)은 InertiaDeceleration 으로 줄어듦.
                    float sideSpeed = math.length(side);

                    if (sideSpeed > math.EPSILON)
                    {
                        side *= math.max(sideSpeed - turnBrake.InertiaDeceleration * deltaTime, 0.0F) / sideSpeed;
                    }

                    velocity = nextForward * along + side;
                }

                playerMovement.ValueRW.Velocity = velocity;
                transform.ValueRW.Rotation = SpaceshipHeading.ToRotation(nextForward);
                movement.ValueRW.DeltaMovement = velocity;
            }
        }
    }
}