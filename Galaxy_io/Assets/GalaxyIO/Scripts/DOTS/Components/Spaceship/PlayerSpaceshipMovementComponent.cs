using GalaxyIO.DOTS.Systems;
using Unity.Entities;
using Unity.Mathematics;
using Unity.NetCode;

namespace GalaxyIO.DOTS.Components
{
    /// <summary>
    /// 플레이어가 조종하는 우주선의 비행 모델. <see cref="PlayerMovementSystem"/>이
    /// 이 값들과 입력을 합쳐 <see cref="SpaceshipMovementComponent"/>를 만듦.
    /// </summary>
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

        // 현재 비행 속력. 예측 롤백 시 서버 값으로 되감아야 하므로 복제함.
        [GhostField]
        public float Speed;

        // 현재 호버링 속도. 기수와 무관한 방향이라 벡터로 들고 있음.
        // 예측 롤백 시 서버 값으로 되감아야 하므로 복제함.
        [GhostField]
        public float2 HoveringVelocity;

        // 가속도
        public float Acceleration;

        // 감속도. 가속 키를 떼면 이 값으로 0까지 감속함.
        public float Deceleration;

        // 회전속도(도/초)
        public float Maneuver;

        // 고속 선회 억제: 최대 속력일 때 Maneuver가 감소하는 비율(0 ~ 1).
        // 0이면 속력과 무관하게 항상 최대 회전속도, 1이면 최대 속력에서 선회 불가.
        public float ManeuverPenalty;
    }
}