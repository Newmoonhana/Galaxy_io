using GalaxyIO.Authoring;
using GalaxyIO.Systems;
using Unity.Entities;
using Unity.Mathematics;

namespace GalaxyIO.Components
{
    /// <summary>
    /// <see cref="SpaceshipAuthoring"/>의 이동과 관련된 DOTS 구조체.
    /// <see cref="SpaceshipMovementSystem"/>에서 접근하기 위해 이동 관련된 필드만 추출함.
    /// </summary>
    public struct SpaceshipMovementComponent : IComponentData
    {
        // 월드 좌표 상 최대 이동속도
        public float MaxSpeed;

        // 현재 이동 속도
        public float Speed;

        // 가속도
        public float Acceleration;

        // 회전속도(도/초)
        public float Maneuver;

        // 고속 선회 억제: 최대 속력일 때 Maneuver가 감소하는 비율(0 ~ 1).
        // 0이면 속력과 무관하게 항상 최대 회전속도, 1이면 최대 속력에서 선회 불가.
        public float ManeuverPenalty;

        // 선회 제동: 이번 프레임에 실제로 회전한 각도(라디안)당 감소하는 속력 비율.
        // 0이면 선회해도 속력이 줄지 않는다.
        public float TurnBrake;

        // Vector2와 같은 유니티에서 쓰는 타입보다 Burst컴파일러에
        // 최적화된 Unity.Mathematics에 포함된 구조체를 써야함.
        // 따라서 Vector2 대신 float2 사용.

        // Forward 벡터
        public float2 Forward;
    }
}