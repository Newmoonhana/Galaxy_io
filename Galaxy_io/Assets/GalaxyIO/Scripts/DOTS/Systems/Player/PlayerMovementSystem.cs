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

                float currentSpeed = playerMovement.ValueRO.Speed;
                float maxSpeed = playerMovement.ValueRO.MaxSpeed;

                // 회전: 목표 방향까지 Maneuver 한계 내에서 접근.
                float2 currentForward = math.normalizesafe(
                    SpaceshipHeading.FromRotation(transform.ValueRO.Rotation),
                    new float2(1.0F, 0.0F)
                );

                float2 desiredForward = math.normalizesafe(desiredDirection, currentForward);

                // 급선회 방지. 속도가 빠를수록 Maneuver를 감쇄시킴.
                float speedRatio = maxSpeed > math.EPSILON ? math.saturate(currentSpeed / maxSpeed) : 0.0F;
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

                // 가속 키를 누르면 MaxSpeed로, 떼면 0으로 접근.
                float desiredSpeed = accelerating ? maxSpeed : 0.0F;
                float maxSpeedDelta = deltaTime * (accelerating
                    ? playerMovement.ValueRO.Acceleration
                    : playerMovement.ValueRO.Deceleration);

                float nextSpeed = currentSpeed + math.clamp(desiredSpeed - currentSpeed, -maxSpeedDelta, maxSpeedDelta);

                // 선회 제동: 이번 틱의 회전속도(도/초)가 빠를수록 감속도가 커짐.
                // 비행 속력이 StartSpeed 를 넘는 만큼 선형으로 더 커짐.
                float turnRate = math.degrees(math.abs(math.atan2(
                    currentForward.x * nextForward.y - currentForward.y * nextForward.x,
                    math.dot(currentForward, nextForward)
                ))) / deltaTime;

                float turnDeceleration = turnBrake.Deceleration
                                         * math.pow(turnRate / turnBrake.TurnRate, turnBrake.Exponent)
                                         * math.max(currentSpeed - turnBrake.StartSpeed, 0.0F)
                                         / (turnBrake.ReferenceSpeed - turnBrake.StartSpeed);

                float brakedSpeed = math.min(turnDeceleration * deltaTime, nextSpeed);
                nextSpeed -= brakedSpeed;

                // 회전 관성: 선회 제동으로 잃은 속력만큼 돌기 전 방향으로 밀려남. 밀려나는 속도는 스스로 줄어듦.
                float2 inertia = playerMovement.ValueRO.InertiaVelocity;
                float inertiaSpeed = math.length(inertia);

                if (inertiaSpeed > math.EPSILON)
                {
                    inertia *= math.max(inertiaSpeed - turnBrake.InertiaDeceleration * deltaTime, 0.0F) / inertiaSpeed;
                }

                inertia += currentForward * (brakedSpeed * turnBrake.Inertia);

                // 호버링: 기수와 무관하게 WASD 합산 방향으로 평행 이동함.
                // 비행 모드가 우선이라, 가속 중이거나 비행 속력이 아직 호버링 속력보다
                // 빠른 동안에는 WASD를 (0, 0)으로 봄.
                float hoveringSpeed = playerMovement.ValueRO.HoveringSpeed;

                float2 desiredHovering = accelerating || nextSpeed > hoveringSpeed
                    ? float2.zero
                    : math.normalizesafe(inputRequest.ValueRO.MoveArrows, float2.zero) * hoveringSpeed;

                bool turning = playerMovement.ValueRO.HoveringTurning;

                float2 nextHovering = SpaceshipHovering.Approach(
                    playerMovement.ValueRO.HoveringVelocity,
                    desiredHovering,
                    playerMovement.ValueRO.HoveringAcceleration,
                    playerMovement.ValueRO.HoveringDeceleration,
                    playerMovement.ValueRO.HoveringManeuver,
                    ref turning,
                    deltaTime
                );

                playerMovement.ValueRW.Speed = nextSpeed;
                playerMovement.ValueRW.HoveringVelocity = nextHovering;
                playerMovement.ValueRW.HoveringTurning = turning;
                playerMovement.ValueRW.InertiaVelocity = inertia;
                transform.ValueRW.Rotation = SpaceshipHeading.ToRotation(nextForward);
                movement.ValueRW.DeltaMovement = nextForward * nextSpeed + nextHovering + inertia;
            }
        }
    }
}