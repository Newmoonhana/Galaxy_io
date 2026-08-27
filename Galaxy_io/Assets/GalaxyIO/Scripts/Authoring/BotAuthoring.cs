using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

namespace GalaxyIO
{
    /// <summary>
    /// Sits on the bot prefab. The prefab also carries a MeshFilter (unit quad) and a
    /// MeshRenderer (white unlit material), which Entities Graphics bakes into the render
    /// components; this baker only adds the gameplay data.
    /// </summary>
    [AddComponentMenu("ECS Simulator/Bot Authoring")]
    [DisallowMultipleComponent]
    public class BotAuthoring : MonoBehaviour { }

    public class BotBaker : Baker<BotAuthoring>
    {
        public override void Bake(BotAuthoring authoring)
        {
            var entity = GetEntity(TransformUsageFlags.Dynamic);
            AddComponent<BotTag>(entity);
            AddComponent(entity, new Velocity { Value = float2.zero });
            AddComponent(entity, new BoxSize { Value = 1f });
        }
    }
}