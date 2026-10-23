using GalaxyIO.DOTS.Authoring;
using Unity.Entities;

namespace GalaxyIO.DOTS.Components.Actor
{
    /// <summary>
    /// <see cref="SpaceshipSpecAsset"/>의 선회 제동, 회전 관성 값. 플레이어 우주선만.
    /// </summary>
    public struct TurnBrakeComponent : IComponentData
    {
        // 기준 회전속도(도/초)
        public float TurnRate;

        // 기준 속력에서 기준 회전속도로 돌 때의 감속도
        public float Deceleration;

        // 곡선의 휘는 정도. 1이면 직선.
        public float Exponent;

        // 제동이 걸리기 시작하는 비행 속력
        public float StartSpeed;

        // 기준 속력. 항상 StartSpeed 보다 큼.
        public float ReferenceSpeed;

        // 선회 제동이 셀수록 속도가 기수를 덜 따라 도는 정도
        public float Inertia;

        // 기수와 어긋난 옆 방향 속도의 감속도
        public float InertiaDeceleration;

        // 뒤처진 속도 방향이 기수 방향을 따라 도는 속도(도/초)
        public float InertiaTurnRate;
    }
}
