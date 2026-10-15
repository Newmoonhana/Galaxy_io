using Unity.Entities;

namespace GalaxyIO.DOTS.Components.Actor
{
    /// <summary>
    /// 접속의 Ghost 복제 반경. 탄 우주선의 레이더 탐지 거리(강화 포함). 우주선이 없으면 마지막 값을 유지함. 서버 전용.
    /// </summary>
    public struct RelevancyRadiusComponent : IComponentData
    {
        public float Value;
    }
}
