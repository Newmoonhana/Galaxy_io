using GalaxyIO.Components;
using Unity.Entities;
using UnityEngine;
using UnityEngine.InputSystem;

namespace GalaxyIO.Authoring
{
    public sealed class PlayerControlAuthoring : MonoBehaviour
    {
        [SerializeField]
        public InputActionAsset InputActionAsset;

        // 최대 열기. Max에 도달하면 한동안 무기 발사 못 함.
        [SerializeField]
        public float MaxHeat;
    }

    public sealed class PlayerControlBaker : Baker<PlayerControlAuthoring>
    {
        public override void Bake(PlayerControlAuthoring authoring)
        {
            Entity entity = GetEntity(TransformUsageFlags.None); // PlayerControl은 단순한 싱글톤 객체이므로 None.

            if (authoring.InputActionAsset != null)
            {
                DependsOn(authoring.InputActionAsset);
            }

            // `InputActionAsset`을 전달해 주는 방법. `PlayerInputSystem`에서 이를 받아 처리함
            AddComponentObject(entity, new PlayerInputInitializer
            {
                InputAction = authoring.InputActionAsset,
                MaxHeat = authoring.MaxHeat,
            });
        }
    }
}