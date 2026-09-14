using GalaxyIO.DOTS.Systems;
using Unity.Entities;
using Unity.Mathematics;

namespace GalaxyIO.DOTS.Components.Actor
{
    /// <summary>
    /// 우주선이 이번 틱에 실제로 움직이는 양. 조종 주체(플레이어/AI)와 무관하게
    /// <see cref="SpaceshipMovementSystem"/>이 Transform에 반영함.
    /// </summary>
    public struct SpaceshipMovementComponent : IComponentData
    {
        // 월드 좌표 상 초당 이동 벡터. 움직임 연산 로직은 다른 시스템에서
        // 수행하고, 이 컴포넌트는 한꺼번에 지정된 DeltaMovement만큼
        // 업데이트함.
        public float2 DeltaMovement;
    }
}