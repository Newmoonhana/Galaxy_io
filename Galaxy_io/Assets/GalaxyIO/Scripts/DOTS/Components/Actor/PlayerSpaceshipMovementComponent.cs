using GalaxyIO.DOTS.Systems;
using Unity.Entities;
using Unity.Mathematics;
using Unity.NetCode;

namespace GalaxyIO.DOTS.Components.Actor
{
    /// <summary>
    /// 플레이어가 조종하는 우주선의 비행 모델. <see cref="PlayerMovementSystem"/>이
    /// 이 값들과 입력을 합쳐 <see cref="MovementComponent"/>를 고침.
    /// 주인에게만 복제함. 다른 클라이언트는 위치와 회전만 받아 보간함.
    /// </summary>
    [GhostComponent(OwnerSendType = SendToOwnerType.SendToOwner)]
    public struct PlayerSpaceshipMovementComponent : IComponentData
    {
        // 비행 모드(가속 키)의 최대 이동속도
        public float MaxSpeed;

        // 호버링 모드(WASD)의 최대 이동속도
        public float HoveringSpeed;

        // 호버링 가속도. WASD를 누르면 이 값으로 목표 속도까지 붙음.
        public float HoveringAcceleration;

        // 호버링 감속도. WASD를 떼면 이 값으로 0까지 줄어듦.
        public float HoveringDeceleration;

        // 가속도
        public float Acceleration;

        // 감속도. 가속 키를 떼면 이 값으로 0까지 감속함.
        public float Deceleration;

        // 회전속도(도/초)
        public float Maneuver;

        // 회전 가속도, 감속도(도/초²)
        public float ManeuverAcceleration;
        public float ManeuverDeceleration;

        // 현재 회전속도(도/초). +는 반시계 방향. 예측 롤백 시 되감아야 하므로 복제함.
        [GhostField]
        public float AngularVelocity;

        // 고속 선회 억제: 최대 속력일 때 Maneuver가 감소하는 비율(0 ~ 1).
        // 0이면 속력과 무관하게 항상 최대 회전속도, 1이면 최대 속력에서 선회 불가.
        public float ManeuverPenalty;

        // 호버링 방향 전환 가속도. 움직이던 중에 WASD 방향을 바꾸면 호버링 가감속 대신 이 값으로 붙음.
        public float HoveringManeuver;

        // 비행 모드에서 이 속력 이상이면 다른 우주선과 부딪히지 않음. 0 이면 항상 부딪힘.
        public float PhaseSpeed;

        // 비행 모드인지. 호버링 모드면 false. PlayerMovementSystem 이 매 틱 정함.
        public bool Flying;

        /// <summary>
        /// 주어진 속도에서 다른 우주선과 부딪히지 않는 상태인지. 호버링 모드에서는 항상 부딪힘.
        /// </summary>
        public bool IsPhasing(float2 velocity)
        {
            return Flying && PhaseSpeed > 0.0F && math.lengthsq(velocity) >= PhaseSpeed * PhaseSpeed;
        }
    }
}