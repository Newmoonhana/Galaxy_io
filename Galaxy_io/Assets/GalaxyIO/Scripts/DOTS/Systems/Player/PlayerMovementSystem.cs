using GalaxyIO.DOTS.Components;
using GalaxyIO.DOTS.Client.Components;
using Unity.Entities;
using Unity.Mathematics;
using Unity.NetCode;
using Unity.Transforms;
using UnityEngine;

namespace GalaxyIO.DOTS.Systems
{
    /// <summary>
    /// <see cref="PlayerInputComponent"/>를 읽어들여 플레이어 Spaceship의 기수 방향과 속력을
    /// Update하는 시스템. 클라이언트에서 먼저 업데이트되고 서버가 나중에 검증함.
    /// <p/>
    /// WASD와 마우스 커서는 방향 제어만 담당하고, 속력은 가속 키가 담당함.
    /// </summary>
    [UpdateInGroup(typeof(PredictedSimulationSystemGroup))]
    public partial struct PlayerMovementSystem : ISystem
    {
        // 커서가 기체로부터 이 거리(월드 유닛) 안에 있으면 방향 제어 입력으로 치지 않음.
        private const float CursorDeadzone = 0.5F;

        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<PlayerInputComponent>();
            state.RequireForUpdate<EndSimulationEntityCommandBufferSystem.Singleton>();
        }

        // 플레이어 입력 > 우주선의 Forward, Velocity 수정
        public void OnUpdate(ref SystemState state)
        {
            foreach (
                var (transform, inputRequest, movementComponent) in
                SystemAPI
                    .Query<RefRO<LocalTransform>, RefRO<PlayerInputComponent>, RefRW<SpaceshipMovementComponent>>()
                    .WithAll<Simulate>()
            )
            {
                float deltaTime = SystemAPI.Time.DeltaTime;
                Vector2 wasdMove = inputRequest.ValueRO.MoveArrows;
                float2 desiredDirection;

                // WASD 방향 제어
                if (wasdMove != Vector2.zero)
                {
                    desiredDirection = new float2(wasdMove.x, wasdMove.y);
                }
                else // 마우스 커서 방향 제어
                {
                    float2 cursorPos = inputRequest.ValueRO.MouseCursor;
                    desiredDirection = cursorPos - transform.ValueRO.Position.xy;

                    // 커서가 기체에 겹치면 방향이 튀므로 방향 제어 입력 없음으로 취급.
                    if (math.lengthsq(desiredDirection) < CursorDeadzone * CursorDeadzone)
                    {
                        desiredDirection = float2.zero;
                    }
                }

                float currentSpeed = movementComponent.ValueRO.Speed;
                float maxSpeed = movementComponent.ValueRO.MaxSpeed;

                // 가속 키를 누르면 MaxSpeed로, 떼면 0으로 접근.
                bool accelerating = inputRequest.ValueRO.AccelerationTriggered;
                float desiredSpeed = accelerating ? maxSpeed : 0.0F;
                float maxSpeedDelta = deltaTime * (accelerating
                    ? movementComponent.ValueRO.Acceleration
                    : movementComponent.ValueRO.Deceleration);

                float nextSpeed = currentSpeed + math.clamp(desiredSpeed - currentSpeed, -maxSpeedDelta, maxSpeedDelta);

                // 회전: 목표 방향까지 Maneuver 한계 내에서 접근.
                float2 currentForward = math.normalizesafe(movementComponent.ValueRO.Forward, new float2(1.0F, 0.0F));
                float2 desiredForward = math.normalizesafe(desiredDirection, currentForward);

                float deltaAngle = math.atan2(
                    currentForward.x * desiredForward.y - currentForward.y * desiredForward.x,
                    math.dot(currentForward, desiredForward)
                );

                // 급선회 방지. 속도가 빠를수록 Maneuver를 감쇄시킴.
                float speedRatio = maxSpeed > math.EPSILON ? math.saturate(currentSpeed / maxSpeed) : 0.0F;
                float maneuverScale = math.lerp(
                    1.0F,
                    1.0F - math.saturate(movementComponent.ValueRO.ManeuverPenalty),
                    speedRatio
                );

                float maxAngleDelta = math.radians(movementComponent.ValueRO.Maneuver) * maneuverScale * deltaTime;

                // 실제 회전 각도
                float appliedAngle = math.clamp(deltaAngle, -maxAngleDelta, maxAngleDelta);
                math.sincos(appliedAngle, out float sin, out float cos);

                movementComponent.ValueRW.Forward = new float2(
                    currentForward.x * cos - currentForward.y * sin,
                    currentForward.x * sin + currentForward.y * cos
                );

                // 선회로 인한 속력 제동 TODO: 선회 제동 유지? 있으면 도그파이팅보다 강강술래게임 되버림
                // float turnBrake = math.saturate(math.abs(appliedAngle) * movementComponent.ValueRO.TurnBrake);
                movementComponent.ValueRW.Speed = nextSpeed;// * (1.0F - turnBrake);
            }
        }
    }
}