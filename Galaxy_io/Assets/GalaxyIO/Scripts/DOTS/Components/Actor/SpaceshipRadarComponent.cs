using GalaxyIO.DOTS.Authoring;
using Unity.Entities;

namespace GalaxyIO.DOTS.Components.Actor
{
    /// <summary>
    /// <see cref="SpaceshipAuthoring"/>의 레이더 기본값. 프리팹에 베이킹되므로 복제하지 않음.
    /// 강화로 늘어난 값은 <see cref="UpgradeParamElement"/>에 따로 있음.
    /// </summary>
    public struct SpaceshipRadarComponent : IComponentData
    {
        // 탐지 거리. Ghost 복제 반경으로도 씀.
        public float Range;

        // 스캐너 회전 속도(도/초)
        public float SweepSpeed;

        // 스캐너가 지나간 뒤 점이 나타나기까지의 시간(초)
        public float ResponseTime;
    }
}
