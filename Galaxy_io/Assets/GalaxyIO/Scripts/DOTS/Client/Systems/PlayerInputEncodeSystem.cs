using GalaxyIO.DOTS.Client.Authoring;
using GalaxyIO.DOTS.Client.Components.Message;
using GalaxyIO.DOTS.Client.Components.Singleton;
using GalaxyIO.DOTS.Components.Actor;
using Unity.Entities;
using Unity.Mathematics;
using Unity.NetCode;
using Unity.Transforms;
using UnityEngine;
using UnityEngine.InputSystem;

namespace GalaxyIO.DOTS.Client.Systems
{
    /// <summary>
    /// 로컬 디바이스의 Input을 읽어 서버에 보낼
    /// <see cref="PlayerInputComponent"/>를 생성하는 시스템.
    /// </summary>
    [WorldSystemFilter(WorldSystemFilterFlags.ClientSimulation)]
    [UpdateInGroup(typeof(GhostInputSystemGroup))]
    public sealed partial class PlayerInputEncodeSystem : SystemBase
    {
        private UnityEngine.Camera mainCamera;
        private InputActionMap gameplayMapping;
        private InputAction mouseCursor;
        private InputAction wasdAction;
        private InputAction turretAction;
        private InputAction artilleryAction;
        private InputAction accelerationAction;

        private bool fireLastlyPressed;

        private bool initialized;

        private EntityQuery menuOpenQuery;

        protected override void OnCreate()
        {
            RequireForUpdate<PlayerInputComponent>();
            RequireForUpdate<GhostOwnerIsLocal>();

            menuOpenQuery = GetEntityQuery(ComponentType.ReadOnly<GameMenuOpenTag>());
        }

        protected override void OnStartRunning()
        {
            if (!initialized)
            {
                PlayerControlComponent initializer = SystemAPI.ManagedAPI.GetSingleton<PlayerControlComponent>();

                // Asset이 null인 경우: 에디터에서 `InputAction`을 바인딩하지 않음.
                if (initializer.InputAction == null || UnityEngine.Camera.main == null)
                {
                    Debug.LogError(
                        $"{nameof(PlayerControlAuthoring)}에 {nameof(InputActionAsset)}이 지정되지 않았거나 " +
                        $"Main 카메라가 없어 {nameof(PlayerInputEncodeSystem)}을 비활성화합니다."
                    );

                    // 초기화에 실패한 경우: 시스템 비활성화
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
                    accelerationAction = gameplayMapping.FindAction("Acceleration", throwIfNotFound: true);
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
            bool menuOpen = !menuOpenQuery.IsEmpty;

            foreach (var (playerInput, transform) in SystemAPI.Query<RefRW<PlayerInputComponent>, RefRO<LocalTransform>>().WithAll<GhostOwnerIsLocal>())
            {
                // Unfocused 상태, 게임 메뉴가 열린 동안엔 움직이지 않음.
                if (!Application.isFocused || menuOpen)
                {
                    playerInput.ValueRW.MouseCursor = transform.ValueRO.Position.xy;
                    playerInput.ValueRW.MoveArrows = float2.zero;
                }
                else
                {
                    // 기수 방향(마우스)과 호버링 이동(WASD)은 서로 다른 축이라 항상 둘 다 실어 보냄.
                    Vector2 cursorPos = mouseCursor.ReadValue<Vector2>();
                    Vector3 cursorWorldPos = mainCamera.ScreenToWorldPoint(
                        new Vector3(cursorPos.x, cursorPos.y, -mainCamera.transform.position.z)
                    );

                    Vector2 arrowMovement = wasdAction.ReadValue<Vector2>();

                    playerInput.ValueRW.MouseCursor = new float2(cursorWorldPos.x, cursorWorldPos.y);
                    playerInput.ValueRW.MoveArrows = new float2(arrowMovement.x, arrowMovement.y);
                }

                // 게임 메뉴가 열린 동안엔 발사/가속도 막음.
                bool firePressed = !menuOpen && turretAction.ReadValue<float>() > 0.0F;
                playerInput.ValueRW.TurretTriggered = firePressed;

                // 마지막 Fire입력값이 false이면 이번 프레임에 누르기 시작했다는 뜻
                playerInput.ValueRW.TurretTriggeredInThisFrame = firePressed && !fireLastlyPressed;
                fireLastlyPressed = firePressed;

                playerInput.ValueRW.ArtilleryTriggered = !menuOpen && artilleryAction.ReadValue<float>() > 0.0F;
                playerInput.ValueRW.AccelerationTriggered = !menuOpen && accelerationAction.ReadValue<float>() > 0.0F;
            }
        }
    }
}