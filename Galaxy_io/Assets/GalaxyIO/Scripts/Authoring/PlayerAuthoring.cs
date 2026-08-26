using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

namespace GalaxyIO
{
    /// <summary>
    /// Sits on the player prefab. Same idea as <see cref="BotAuthoring"/>: the render
    /// components come from the MeshFilter/MeshRenderer, the baker adds the gameplay data.
    /// </summary>
    [AddComponentMenu("ECS Simulator/Player Authoring")]
    [DisallowMultipleComponent]
    public class PlayerAuthoring : MonoBehaviour { }

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