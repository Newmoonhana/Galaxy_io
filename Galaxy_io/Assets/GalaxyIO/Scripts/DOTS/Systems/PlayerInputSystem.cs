using GalaxyIO.Authoring;
using GalaxyIO.Components;
using GalaxyIO.Components.Tags;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;
using UnityEngine.InputSystem;

namespace GalaxyIO.Systems
{
    /// <summary>
    /// Managed 오브젝트인 <see cref="InputActionAsset"/>에서 사용자 입력을 추출하여
    /// Unmanaged 오브젝트인 <see cref="PlayerInputState"/>로 변환하는 System.
    /// </summary>
    [UpdateInGroup(typeof(InitializationSystemGroup))]
    public sealed partial class PlayerInputSystem : SystemBase
    {
        private InputActionMap _gameplayMapping;
        private InputAction _mouseCursor;
        private InputAction _wasdMove;
        private InputAction _fire;
        private InputAction _boost;

        private float heat;
        private float maxHeat;

        private bool initialized;

        // 화면 좌표 -> 월드 좌표 변환에 사용하는 카메라 캐시.
        private UnityEngine.Camera mainCamera;

        protected override void OnCreate()
        {
            RequireForUpdate<PlayerInputInitializer>();
            RequireForUpdate(SystemAPI.QueryBuilder().WithAll<LocalPlayerTag>().Build());
        }

        protected override void OnStartRunning()
        {
            if (!initialized)
            {
                PlayerInputInitializer initializer = SystemAPI.ManagedAPI.GetSingleton<PlayerInputInitializer>();

                // Asset이 null인경우: Editor에서 `PlayerControlAuthoring` 컴포넌트에 `InputAction` 바인딩 안한 경우
                if (initializer.InputAction == null)
                {
                    Debug.LogError(
                        $"{nameof(PlayerControlAuthoring)}에 {nameof(InputActionAsset)}이 지정되지 않아 " +
                        $"{nameof(PlayerInputSystem)}을 비활성화합니다."
                    );

                    // `InputAction` 정상적으로 초기화되지 않은 경우: 시스템 비활성화
                    Enabled = false;
                }
                else
                {
                    maxHeat = initializer.MaxHeat;
                    _gameplayMapping = initializer.InputAction.FindActionMap("Gameplay", throwIfNotFound: true);
                    _mouseCursor = _gameplayMapping.FindAction("Move_By_Mouse", throwIfNotFound: true);
                    _wasdMove = _gameplayMapping.FindAction("Move_By_Buttons", throwIfNotFound: true);
                    _fire = _gameplayMapping.FindAction("Fire", throwIfNotFound: true);
                    _boost = _gameplayMapping.FindAction("Boost", throwIfNotFound: true);
                }

                initialized = true;
            }

            _gameplayMapping?.Enable();
        }

        protected override void OnStopRunning()
        {
            _gameplayMapping?.Disable();
        }

        // 플레이어 입력 > 우주선의 Forward, Velocity 수정
        protected override void OnUpdate()
        {
            Entity entity = SystemAPI.GetSingletonEntity<LocalPlayerTag>();
            var movementComponent = SystemAPI.GetComponentRW<MovementComponent>(entity);
            var transformComponent = SystemAPI.GetComponentRO<LocalTransform>(entity);
            float deltaTime = SystemAPI.Time.DeltaTime;
            Vector2 wasdMove = _wasdMove.ReadValue<Vector2>();
            float2 desiredDirection;
            float desiredSpeed;

            // WASD이동
            if (wasdMove != Vector2.zero)
            {
                desiredDirection = new float2(wasdMove.x, wasdMove.y);
                // WASD이동은 항상 최대 속력
                desiredSpeed = movementComponent.ValueRO.MaxSpeed;
            }
            else // 마우스 커서로 이동
            {
                Vector2 cursorPos = _mouseCursor.ReadValue<Vector2>();
                UnityEngine.Camera camera = GetMainCamera();

                if (camera == null)
                {
                    // 카메라가 없으면 화면 좌표를 월드 좌표로 변환할 수 없음. 방향 유지 + 감속.
                    desiredDirection = movementComponent.ValueRO.Forward;
                    desiredSpeed = 0.0F;
                }
                else
                {
                    float2 screenCenter = new float2(Screen.width, Screen.height) * 0.5F;

                    // 목표 방향: `화면 중심 -> 커서 위치`
                    float depth = transformComponent.ValueRO.Position.z - camera.transform.position.z;
                    Vector3 cursorWorld = camera.ScreenToWorldPoint(new Vector3(cursorPos.x, cursorPos.y, depth));
                    Vector3 centerWorld = camera.ScreenToWorldPoint(new Vector3(screenCenter.x, screenCenter.y, depth));

                    desiredDirection = new float2(
                        cursorWorld.x - centerWorld.x,
                        cursorWorld.y - centerWorld.y
                    );

                    // 화면 중심 -> 마우스 포인터 거리에 비례한 속력 감쇄.
                    float maxCursorDistance = math.max(math.cmin(screenCenter) / 0.5F, 1.0F);
                    float throttle = math.saturate(
                        math.distance(new float2(cursorPos.x, cursorPos.y), screenCenter) / maxCursorDistance
                    );

                    desiredSpeed = movementComponent.ValueRO.MaxSpeed * throttle;
                }
            }

            // 가속: 목표 속력까지 Acceleration 한계 내에서 접근.
            float currentSpeed = movementComponent.ValueRO.Speed;
            float maxSpeed = movementComponent.ValueRO.MaxSpeed;
            float maxSpeedDelta = movementComponent.ValueRO.Acceleration * deltaTime;
            float nextSpeed = currentSpeed + math.clamp(desiredSpeed - currentSpeed, -maxSpeedDelta, maxSpeedDelta);

            // 회전: 목표 방향까지 Maneuver 한계 내에서 접근.
            float2 currentForward = math.normalizesafe(movementComponent.ValueRO.Forward, new float2(1.0F, 0.0F));
            float2 desiredForward = math.normalizesafe(desiredDirection, currentForward);

            float deltaAngle = math.atan2(
                currentForward.x * desiredForward.y - currentForward.y * desiredForward.x,
                math.dot(currentForward, desiredForward)
            );

            // 빠른 속도로 이동중에 빠른 선회를 방지하기 위해 현재 속도에 비례하여 Maneuver를
            // 감쇄시킴. 정지 상태에 가까울수록 원래의 Maneuver를 유지하며 회전.
            float speedRatio = maxSpeed > math.EPSILON ? math.saturate(currentSpeed / maxSpeed) : 0.0F;
            float maneuverScale = math.lerp(
                1.0F,
                1.0F - math.saturate(movementComponent.ValueRO.ManeuverPenalty),
                speedRatio
            );

            float maxAngleDelta = math.radians(movementComponent.ValueRO.Maneuver) * maneuverScale * deltaTime;

            // 실제 회전 각도
            float appliedAngle = math.clamp(deltaAngle, -maxAngleDelta, maxAngleDelta);
            math.sincos(appliedAngle, out float sin, out float cos);

            movementComponent.ValueRW.Forward = new float2(
                currentForward.x * cos - currentForward.y * sin,
                currentForward.x * sin + currentForward.y * cos
            );

            // 선회로 인한 속력 제동
            float turnBrake = math.saturate(math.abs(appliedAngle) * movementComponent.ValueRO.TurnBrake);
            movementComponent.ValueRW.Speed = nextSpeed * (1.0F - turnBrake);

            // TODO: 투사체 발사, 부스터
            bool fireRaw = _fire.ReadValue<float>() > 0.0F;
            bool boostRaw = _boost.ReadValue<float>() > 0.0F;
        }

        /// <summary>
        /// 메인 카메라를 캐싱해서 반환한다. 씬 전환 등으로 파괴된 경우 다시 찾는다.
        /// (GalaxyIO.Camera 네임스페이스와 이름이 겹치므로 항상 정규화된 이름을 사용한다.)
        /// </summary>
        private UnityEngine.Camera GetMainCamera()
        {
            if (mainCamera == null)
            {
                mainCamera = UnityEngine.Camera.main;
            }

            return mainCamera;
        }
    }
}