using GalaxyIO.DOTS.Client.Authoring;
using GalaxyIO.DOTS.Client.Components;
using GalaxyIO.DOTS.Components;
using Unity.Entities;
using Unity.Mathematics;
using Unity.NetCode;
using UnityEngine;
using UnityEngine.InputSystem;

namespace GalaxyIO.DOTS.Client.Systems
{
    /// <summary>
    /// 로컬 디바이스의 Input을 처리받아 서버에 보낼
    /// <see cref="PlayerInputComponent"/>를 생성하는 시스템.
    /// </summary>
    [WorldSystemFilter(WorldSystemFilterFlags.ClientSimulation)]
    public sealed partial class PlayerInputEncodeSystem : SystemBase
    {
        private UnityEngine.Camera mainCamera;
        private InputActionMap gameplayMapping;
        private InputAction mouseCursor;
        private InputAction wasdAction;
        private InputAction turretAction;
        private InputAction artilleryAction;
        private InputAction afterburnerAction;

        private bool fireLastlyPressed;

        private bool initialized;

        protected override void OnCreate()
        {
            RequireForUpdate<PlayerInputComponent>();
            RequireForUpdate<GhostOwnerIsLocal>();
        }

        protected override void OnStartRunning()
        {
            if (!initialized)
            {
                PlayerControlComponent initializer = SystemAPI.ManagedAPI.GetSingleton<PlayerControlComponent>();

                // Asset이 null인경우: Editor에서 `PlayerControlAuthoring` 컴포넌트에 `InputAction` 바인딩 안한 경우
                if (initializer.InputAction == null || UnityEngine.Camera.main == null)
                {
                    Debug.LogError(
                        $"{nameof(PlayerControlAuthoring)}에 {nameof(InputActionAsset)}이 지정되지 않았거나 " +
                        $"Main 카메라가 없어 {nameof(PlayerInputEncodeSystem)}을 비활성화합니다."
                    );

                    // `InputAction` 정상적으로 초기화되지 않은 경우: 시스템 비활성화
                    Enabled = false;
                }
                else
                {
                    mainCamera = UnityEngine.Camera.main;
                    gameplayMapping = initializer.InputAction.FindActionMap("Gameplay", throwIfNotFound: true);
                    mouseCursor = gameplayMapping.FindAction("Move_By_Mouse", throwIfNotFound: true);
                    wasdAction = gameplayMapping.FindAction("Move_By_Buttons", throwIfNotFound: true);
                    turretAction = gameplayMapping.FindAction("Turret", throwIfNotFound: true);
                    artilleryAction = gameplayMapping.FindAction("Artillery", throwIfNotFound: true);
                    afterburnerAction = gameplayMapping.FindAction("Afterburner", throwIfNotFound: true);
                }

                initialized = true;
            }

            gameplayMapping?.Enable();
        }

        protected override void OnStopRunning()
        {
            gameplayMapping?.Disable();
        }

        protected override void OnUpdate()
        {
            foreach (var playerInput in SystemAPI.Query<RefRW<PlayerInputComponent>>().WithAll<GhostOwnerIsLocal>())
            {
                Vector2 arrowMovement = wasdAction.ReadValue<Vector2>();

                // WASD 움직임이 감지되면, 마우스 포인터 움직임보다 제어 우선순위를 가짐
                if (arrowMovement != Vector2.zero)
                {
                    playerInput.ValueRW.MoveArrows = new float2(arrowMovement.x, arrowMovement.y);
                }
                else // WASD 움직임 입력이 없을경우 마우스 포인터 움직임으로 대체
                {
                    Vector2 cursorPos = mouseCursor.ReadValue<Vector2>();
                    Vector3 cursorWorldPos = mainCamera.ScreenToWorldPoint(
                        new Vector3(cursorPos.x, cursorPos.y, -mainCamera.transform.position.z)
                    );

                    playerInput.ValueRW.MouseCursor = new float2(cursorWorldPos.x, cursorWorldPos.y);
                    playerInput.ValueRW.MoveArrows = new float2(0, 0); // WASD값 초기화
                }

                bool firePressed = turretAction.ReadValue<float>() > 0.0F;
                playerInput.ValueRW.TurretTriggered = firePressed;

                // 마지막 Fire입력값이 false이면 이번 프레임에 누르기 시작했다는 뜻
                playerInput.ValueRW.TurretTriggeredInThisFrame = firePressed && !fireLastlyPressed;
                fireLastlyPressed = firePressed;

                playerInput.ValueRW.ArtilleryTriggered = artilleryAction.ReadValue<float>() > 0.0F;
                playerInput.ValueRW.AfterburnerTriggered = afterburnerAction.ReadValue<float>() > 0.0F;
            }
        }
    }
}