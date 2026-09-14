using GalaxyIO.DOTS.Client.Systems;
using Unity.Entities;
using UnityEngine.InputSystem;

namespace GalaxyIO.DOTS.Client.Components.Singleton
{
    /// <summary>
    /// 클라이언트의 Managed Object를 <see cref="PlayerInputEncodeSystem"/>에
    /// 전달하는 컴포넌트.
    /// </summary>
    public sealed class PlayerControlComponent : IComponentData
    {
        public InputActionAsset InputAction;
    }
}