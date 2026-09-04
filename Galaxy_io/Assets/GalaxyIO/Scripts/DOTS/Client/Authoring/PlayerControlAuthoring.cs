using GalaxyIO.DOTS.Components;
using Unity.Entities;
using UnityEngine;
using UnityEngine.InputSystem;

namespace GalaxyIO.DOTS.Client.Authoring
{
    /// <summary>
    /// <see cref="InputActionAsset"/>을 System에 전달하는 컴포넌트. 월드당 한 개.
    /// </summary>
    public sealed class PlayerControlAuthoring : MonoBehaviour
    {
        // Input Action 에셋
        [SerializeField]
        public InputActionAsset InputActionAsset;
    }

    public sealed class PlayerControlBaker : Baker<PlayerControlAuthoring>
    {
        public override void Bake(PlayerControlAuthoring authoring)
        {
            Entity entity = GetEntity(TransformUsageFlags.None);

            AddComponentObject(entity, new PlayerControlComponent
            {
                InputAction = authoring.InputActionAsset,
            });
        }
    }
}