using GalaxyIO.DOTS.Components.Actor;
using Unity.Entities;
using UnityEngine;

namespace GalaxyIO.DOTS.Authoring
{
    /// <summary>
    /// 점령전 위성. 자동 포탑 칸 하나를 가진 움직이지 않는 우주선으로 만듦.
    /// </summary>
    [RequireComponent(typeof(SpaceshipAuthoring))]
    public sealed class SatelliteAuthoring : MonoBehaviour
    {
    }

    public sealed class SatelliteBaker : Baker<SatelliteAuthoring>
    {
        public override void Bake(SatelliteAuthoring authoring)
        {
            Entity entity = GetEntity(TransformUsageFlags.Dynamic);
            AddComponent<SatelliteComponent>(entity);
            AddComponent<AlwaysRelevantTag>(entity);
        }
    }
}
