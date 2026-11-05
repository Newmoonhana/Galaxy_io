using UnityEngine;
using UnityEngine.InputSystem;

namespace OOPSample
{
    /// <summary>
    /// ECS 샘플의 <c>PlayerInputSystem</c> + <c>PlayerInputState</c> 대응.
    ///
    /// DOTS 쪽에서는 <see cref="InputActionAsset"/>이 매니지드 오브젝트라 이 시스템만
    /// <c>SystemBase</c>여야 했고, 샘플링 결과를 Unmanaged 싱글톤으로 복사해야만 이후 단계가
    /// Burst 호환으로 남았습니다. OOP에는 그 경계 자체가 없으므로 그냥 평범한 클래스입니다.
    /// 매니지드/언매니지드 변환 계층이 통째로 사라지는 것이 OOP 쪽의 몇 안 되는 이득입니다.
    /// </summary>
    public sealed class PlayerInputReader
    {
        private const string GameplayMapName = "Gameplay";
        private const string MoveActionName = "Move";

        private readonly InputActionMap m_Gameplay;
        private readonly InputAction m_Move;

        /// <summary>HUD의 Text Field가 포커스 되어있을 때, 게임플레이 입력을 비활성화함.</summary>
        public static bool UiHasFocus { get; set; }

        public Vector2 Move { get; private set; }

        public bool IsValid => m_Move != null;

        public PlayerInputReader(InputActionAsset asset)
        {
            if (asset == null)
            {
                Debug.LogError(
                    $"{nameof(SimulationRunner)}에 {nameof(InputActionAsset)}이 지정되지 않아 " +
                    $"플레이어 입력을 비활성화합니다."
                );

                return;
            }

            m_Gameplay = asset.FindActionMap(GameplayMapName, throwIfNotFound: true);
            m_Move = m_Gameplay.FindAction(MoveActionName, throwIfNotFound: true);
        }

        public void Enable() => m_Gameplay?.Enable();

        /// <summary>에셋을 참조할 뿐 소유하지는 않으므로 Dispose하지 않고 비활성화만 합니다.</summary>
        public void Disable() => m_Gameplay?.Disable();

        /// <summary>프레임당 한 번. ECS의 <c>InitializationSystemGroup</c> 자리입니다.</summary>
        public void Sample()
        {
            Move = !UiHasFocus && m_Move != null ? m_Move.ReadValue<Vector2>() : Vector2.zero;
        }
    }
}
