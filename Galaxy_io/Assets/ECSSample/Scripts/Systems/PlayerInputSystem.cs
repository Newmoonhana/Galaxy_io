using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.InputSystem;

namespace GalaxyIO
{
    /// <summary>
    /// Managed 오브젝트인 <see cref="InputActionAsset"/>을 포함하기 때문에 <see cref="SystemBase"/>
    /// 를 상속받음.
    ///
    /// 액션 에셋은 코드로 생성하지 않고 <see cref="SimulationAuthoring"/>의 인스펙터에 지정된 것을
    /// <see cref="PlayerInputAsset"/>을 통해 그대로 받아 사용합니다.
    ///
    /// 이 시스템이 수행하는 작업은 샘플링된 축(axis) 값을 단순 데이터 구조인 <see cref="PlayerInputState"/>
    /// 싱글톤으로 복사하는 것뿐이므로, 이후 단계의 모든 로직은 여전히 Burst 컴파일러와 호환됩니다.
    ///
    /// <c>InitializationSystemGroup</c>: 프레임 당 한번.
    /// </summary>
    [UpdateInGroup(typeof(InitializationSystemGroup))]
    public partial class PlayerInputSystem : SystemBase
    {
        private const string GameplayMapName = "Gameplay";
        private const string MoveActionName = "Move";

        /// <summary>
        /// HUD의 Text Field가 포커스 되어있을 때, 게임플레이 입력을 비활성화함.
        /// </summary>
        public static bool UiHasFocus { get; set; }

        private InputActionMap m_Gameplay;

        private InputAction m_Move;

        protected override void OnCreate()
        {
            RequireForUpdate<PlayerInputState>();
            RequireForUpdate<PlayerInputAsset>();
        }

        protected override void OnStartRunning()
        {
            // SubScene이 로드된 뒤에야 싱글톤이 존재하므로 OnCreate가 아닌 여기서 해석합니다.
            if (m_Move == null && !TryResolveActions())
            {
                Enabled = false;
                return;
            }

            m_Gameplay.Enable();
        }

        protected override void OnStopRunning()
        {
            // 에셋을 직접 참조할 뿐 소유하지는 않으므로 Dispose하지 않고 비활성화만 합니다.
            m_Gameplay?.Disable();
        }

        protected override void OnUpdate()
        {
            var move = float2.zero;

            if (!UiHasFocus)
            {
                var raw = m_Move.ReadValue<Vector2>();
                move = new float2(raw.x, raw.y);
            }

            SystemAPI.SetSingleton(new PlayerInputState { Move = move });
        }

        private bool TryResolveActions()
        {
            var asset = SystemAPI.ManagedAPI.GetSingleton<PlayerInputAsset>().Value;

            if (asset == null)
            {
                Debug.LogError(
                    $"{nameof(SimulationAuthoring)}에 {nameof(InputActionAsset)}이 지정되지 않아 " +
                    $"{nameof(PlayerInputSystem)}을 비활성화합니다."
                );

                return false;
            }

            m_Gameplay = asset.FindActionMap(GameplayMapName, throwIfNotFound: true);
            m_Move = m_Gameplay.FindAction(MoveActionName, throwIfNotFound: true);

            return true;
        }
    }
}