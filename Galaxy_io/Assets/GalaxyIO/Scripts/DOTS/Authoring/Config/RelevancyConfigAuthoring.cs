using GalaxyIO.DOTS.Components.Singleton;
using Unity.Entities;
using UnityEngine;

namespace GalaxyIO.DOTS.Authoring.Config
{
    /// <summary>
    /// Ghost 복제 범위 설정. 월드당 한 개. 반경은 우주선마다의 레이더 탐지 거리(SpaceshipAuthoring.RadarRange)를 씀.
    /// </summary>
    public sealed class RelevancyConfigAuthoring : MonoBehaviour
    {
        [SerializeField]
        [Tooltip("이미 보내던 Ghost 를 추가로 유지하는 거리. 경계에서 깜빡이는 것 방지.")]
        [Min(0.0F)]
        public float Hysteresis = 8.0F;
    }

    public sealed class RelevancyConfigBaker : Baker<RelevancyConfigAuthoring>
    {
        public override void Bake(RelevancyConfigAuthoring authoring)
        {
            Entity entity = GetEntity(TransformUsageFlags.None);

            AddComponent(entity, new RelevancyConfigComponent
            {
                Hysteresis = authoring.Hysteresis,
            });
        }
    }
}
