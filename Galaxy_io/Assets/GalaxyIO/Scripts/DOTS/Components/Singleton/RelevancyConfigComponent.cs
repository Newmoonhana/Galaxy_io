using GalaxyIO.DOTS.Authoring.Config;
using Unity.Entities;

namespace GalaxyIO.DOTS.Components.Singleton
{
    /// <summary>
    /// <see cref="RelevancyConfigAuthoring"/>의 DOTS 구조체. 월드당 한 개.
    /// </summary>
    public struct RelevancyConfigComponent : IComponentData
    {
        // 끄면 모든 Ghost 를 모든 접속에 보냄.
        public bool UseRelevancy;

        // 접속 초점에서 이 반경 안의 Ghost 만 보냄.
        public float Radius;

        // 이미 보내던 Ghost 는 Radius + Hysteresis 까지 유지함.
        public float Hysteresis;
    }
}
