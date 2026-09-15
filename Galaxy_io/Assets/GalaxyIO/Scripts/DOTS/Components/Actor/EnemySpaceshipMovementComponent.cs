using GalaxyIO.DOTS.Authoring;
using Unity.Entities;
using Unity.Mathematics;

namespace GalaxyIO.DOTS.Components.Actor
{
    /// <summary>
    /// AI 가 조종하는 우주선의 비행 모델. 플레이어의 호버링(WASD)과 같은 방식으로 움직임.
    /// 값은 <see cref="SpaceshipAuthoring"/>에서 가져옴.
    /// </summary>
    public struct EnemySpaceshipMovementComponent : IComponentData
    {
        // 최대 이동속도
        public float HoveringSpeed;

        // 가속도. 목표 속도가 있으면 이 값으로 접근.
        public float HoveringAcceleration;

        // 감속도. 목표 속도가 (0, 0)이면 이 값으로 줄어듦.
        public float HoveringDeceleration;

        // 회전속도(도/초)
        public float Maneuver;

        // 현재 속도. 기수와 무관한 방향.
        public float2 Velocity;
    }
}