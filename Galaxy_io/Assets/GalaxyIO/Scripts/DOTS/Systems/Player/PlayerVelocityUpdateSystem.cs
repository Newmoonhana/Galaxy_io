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
    /// 클라이언트 Input을 매 프레임 캡쳐하는 <see cref="PlayerInputComponent"/>를
    /// 읽어들여 플레이어 Spaceship의 Transform을 Update하는 시스템.
    ///
    /// PredictedSimulationSystemGroup—Transform이 클라이언트에서 먼저 업데이트되고,
    /// 서버에서 이를 나중에 검증한다.
    /// </summary>
    [UpdateInGroup(typeof(PredictedSimulationSystemGroup))]
    public partial struct PlayerVelocityUpdateSystem : ISystem
    {
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
            )
            {
                float deltaTime = SystemAPI.Time.DeltaTime;
                Vector2 wasdMove = inputRequest.ValueRO.MoveArrows;
                float2 desiredDirection;
                float desiredSpeed;

                // WASD이동
                if (wasdMove != Vector2.zero)
                {
                    desiredDirection = new float2(wasdMove.x, wasdMove.y);
                    // WASD이동은 항상 최대 속력
                    desiredSpeed = movementComponent.ValueRO.MaxSpeed;
                }
                else // 마우스 커서로 이동
                {
                    float2 cursorPos = inputRequest.ValueRO.MouseCursor;
                    desiredDirection = cursorPos - transform.ValueRO.Position.xy;

                    // 마우스 포인터 위치에 따른 감속
                    float throttle = math.min(1.0F, math.length(desiredDirection) / 4.0F);
                    desiredSpeed = movementComponent.ValueRO.MaxSpeed * throttle;
                }

                // 가속: 목표 속력까지 Acceleration 한계 내에서 접근.
                float currentSpeed = movementComponent.ValueRO.Speed;
                float maxSpeed = movementComponent.ValueRO.MaxSpeed;
                float maxSpeedDelta = movementComponent.ValueRO.Acceleration * deltaTime;
                float nextSpeed = currentSpeed + math.clamp(desiredSpeed - currentSpeed, -maxSpeedDelta, maxSpeedDelta);

                // 회전: 목표 방향까지 Maneuver 한계 내에서 접근.
                float2 currentForward = math.normalizesafe(movementComponent.ValueRO.Forward, new float2(1.0F, 0.0F));
                float2 desiredForward = math.normalizesafe(desiredDirection, currentForward);

                float deltaAngle = math.atan2(
                    currentForward.x * desiredForward.y - currentForward.y * desiredForward.x,
                    math.dot(currentForward, desiredForward)
                );

                // 빠른 속도로 이동중에 급선회를 방지하기 위해 현재 속도에 비례하여 Maneuver를
                // 감쇄시킴. 정지 상태에 가까울수록 원래의 Maneuver를 유지하며 회전.
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