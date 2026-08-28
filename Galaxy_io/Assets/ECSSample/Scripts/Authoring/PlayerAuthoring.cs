using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

namespace GalaxyIO
{
    /// <summary>
    /// 플레이어 Prefab => Entity 변환기
    /// </summary>
    [AddComponentMenu("ECS Simulator/Player Authoring")]
    [DisallowMultipleComponent]
    public class PlayerAuthoring : MonoBehaviour {}

    public class PlayerBaker : Baker<PlayerAuthoring>
    {
        public override void Bake(PlayerAuthoring authoring)
        {
            var entity = GetEntity(TransformUsageFlags.Dynamic);
            AddComponent<PlayerTag>(entity);
            AddComponent(entity, new Velocity { Value = float2.zero });
            AddComponent(entity, new BoxSize { Value = 1f });
        }
    }
}