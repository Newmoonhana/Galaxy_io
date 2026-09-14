using GalaxyIO.DOTS.Authoring;
using Unity.Entities;
using Unity.Mathematics;

namespace GalaxyIO.DOTS.Components.Actor
{
    /// <summary>
    /// <see cref="EnemyAuthoring"/>의 DOTS 구조체. 터렛 RPS 와 별개로 도는 우주선 단위 사격/휴식 주기.
    /// 모든 터렛이 이 주기 하나를 공유함.
    /// </summary>
    public struct EnemyTurretComponent : IComponentData
    {
        // 한 번에 사격하는 시간(초)
        public float FireDuration;

        // 사격 후 휴식 시간 추출 범위(초). (최소, 최대)
        public float2 RestDurationRange;

        // 사격을 허용하는 조준 오차. 기수와 대상 방향 사이 각도의 cos 값.
        public float AimCosine;

        // 현재 주기(사격/휴식)의 남은 시간
        public float CycleRemaining;

        // 휴식 중인지
        public bool Resting;
    }
}