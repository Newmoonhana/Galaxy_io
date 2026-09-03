using GalaxyIO.DOTS.Client.Systems;
using Unity.Entities;
using UnityEngine.InputSystem;

namespace GalaxyIO.DOTS.Components
{
    /// <summary>
    /// 시뮬레이션 시작 시 클라이언트의 Managed Object들을
    /// <see cref="PlayerInputEncodeSystem"/>에 전달해줄
    /// 컴포넌트
    /// </summary>
    public sealed class PlayerControlComponent : IComponentData
    {
        public InputActionAsset InputAction;
    }
}