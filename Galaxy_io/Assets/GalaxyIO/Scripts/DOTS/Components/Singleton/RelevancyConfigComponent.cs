using GalaxyIO.DOTS.Authoring.Config;
using Unity.Entities;

namespace GalaxyIO.DOTS.Components.Singleton
{
    /// <summary>
    /// <see cref="RelevancyConfigAuthoring"/>의 DOTS 구조체. 월드당 한 개.
    /// </summary>
    public struct RelevancyConfigComponent : IComponentData
    {
        // 이미 보내던 Ghost 는 복제 반경 + Hysteresis 까지 유지함.
        public float Hysteresis;
    }
}
