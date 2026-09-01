using Unity.Entities;
using UnityEngine.InputSystem;

namespace GalaxyIO.Components
{
    /// <summary>
    /// System 시작 시 `InputActionAsset` 건네 주는 용도.
    /// </summary>
    public sealed class PlayerInputInitializer : IComponentData
    {
        public InputActionAsset InputAction;
        public float MaxHeat;
    }
}