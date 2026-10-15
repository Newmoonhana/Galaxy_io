using GalaxyIO.DOTS.Components.Singleton;
using Unity.Entities;
using UnityEngine;

namespace GalaxyIO.DOTS.Authoring.Config
{
    /// <summary>
    /// 플레이어 스폰 설정. 월드당 한 개.
    /// </summary>
    public sealed class PlayerSpawnConfigAuthoring : MonoBehaviour
    {
        [SerializeField]
        [Tooltip("최초 스폰 / 리스폰 때 타는 우주선. 프리팹 레지스트리에 등록된 Id 여야 함.")]
        public PrefabId DefaultSpaceship = PrefabId.Vex;
    }

    public sealed class PlayerSpawnConfigBaker : Baker<PlayerSpawnConfigAuthoring>
    {
        public override void Bake(PlayerSpawnConfigAuthoring authoring)
        {
            Entity entity = GetEntity(TransformUsageFlags.None);

            AddComponent(entity, new PlayerSpawnConfigComponent
            {
                DefaultSpaceship = authoring.DefaultSpaceship,
            });
        }
    }
}
