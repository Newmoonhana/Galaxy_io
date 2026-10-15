using GalaxyIO.DOTS.Systems;
using Unity.Entities;
using Unity.Mathematics;

namespace GalaxyIO.DOTS.Components.Actor
{
    /// <summary>
    /// <see cref="StrafingBehaviorComponent"/>의 상태.
    /// </summary>
    public enum StrafingStateID : byte
    {
        // 바라보는 방향으로 느리게 배회. 대상 탐색 중.
        Idle = 0,

        // 대상을 바라보며 좌/우로 이동. 멀면 추격.
        Engaging = 1,
    }

    /// <summary>
    /// Strafing AI 의 행동 데이터. <see cref="StrafingBehaviorSystem"/>이 사용함.
    /// </summary>
    public struct StrafingBehaviorComponent : IComponentData
    {
        public StrafingStateID State;

        // Idle 이동속도
        public float IdleSpeed;

        // 한 번에 좌/우로 이동할 거리 추출 범위. (최소, 최대)
        public float2 StrafeDistanceRange;

        // 대상 선체 바깥이 이보다 멀면 좌/우 이동을 멈추고 추격함.
        public float ChaseRadius;

        // 현재 좌/우 방향. 1: 대상 기준 왼쪽, -1: 오른쪽.
        public float StrafeDirection;

        // 현재 방향으로 남은 이동 거리. 0 이하가 되면 다시 뽑음.
        public float StrafeRemaining;

        // Idle 진입 시 뽑은 배회 방향. 스폰 직후엔 (0, 0).
        public float2 WanderDirection;
    }
}