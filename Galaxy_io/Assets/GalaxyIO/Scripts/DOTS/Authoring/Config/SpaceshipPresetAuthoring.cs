using GalaxyIO.DOTS.Components.Config;
using Unity.Entities;
using UnityEngine;

namespace GalaxyIO.DOTS.Authoring.Config
{
    /// <summary>
    /// 스폰에 사용할 Spaceship 프리팹 모음. 월드당 한 개.
    /// </summary>
    public sealed class SpaceshipPresetAuthoring : MonoBehaviour
    {
        // 플레이어 최초 스폰시의 기본 Spaceship
        [SerializeField]
        public GameObject PlayerDefaultShip;
    }

    public sealed class SpaceshipPresetBaker : Baker<SpaceshipPresetAuthoring>
    {
        public override void Bake(SpaceshipPresetAuthoring authoring)
        {
            Entity entity = GetEntity(TransformUsageFlags.None);

            AddComponent(entity, new SpaceshipPresetComponent
            {
                PlayerDefaultShip = GetEntity(authoring.PlayerDefaultShip, TransformUsageFlags.Dynamic)
            });
        }
    }
}