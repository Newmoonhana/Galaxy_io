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

        // 탄 퍼짐 계산에 사용하는 난수 생성기.
        private Unity.Mathematics.Random random;

        private bool initialized;

        // 화면 좌표 -> 월드 좌표 변환에 사용하는 카메라 캐시.
        private UnityEngine.Camera mainCamera;

        protected override void OnCreate()
        {
            // Random의 시드는 0이 될 수 없음.
            random = Unity.Mathematics.Random.CreateFromIndex((uint)System.DateTime.Now.Ticks & 0x7FFFFFFFu);

            RequireForUpdate<PlayerInputInitializer>();
            RequireForUpdate<EndSimulationEntityCommandBufferSystem.Singleton>();
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
            var movementComponent = SystemAPI.GetComponentRW<SpaceshipMovementComponent>(entity);
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

            DynamicBuffer<TurretElement> turrets = EntityManager.GetBuffer<TurretElement>(entity);
            bool firePressed = _fire.ReadValue<float>() > 0.0F;

            // 발사되었을때만 commandBuffer가 필요하므로 최적화
            var commandBuffer = firePressed ? SystemAPI
                .GetSingleton<EndSimulationEntityCommandBufferSystem.Singleton>()
                .CreateCommandBuffer(World.Unmanaged) : new EntityCommandBuffer();

            // ref참조를 위해 foreach가 아닌 for로 접근
            for (int i = 0; i < turrets.Length; i++)
            {
                ref TurretElement turret = ref turrets.ElementAt(i);

                // 투사체가 없는 터렛
                if (turret.ProjectilePrefab == Entity.Null)
                {
                    continue;
                }

                // 발사 재사용 대기중인 터렛
                if (turret.FireCooldown > 0.0F)
                {
                    turret.FireCooldown -= deltaTime;
                    continue;
                }

                // 발사 누를시에만 투사체를 발사함.
                // FireCooldown때문에 Early-exit 불가.
                if (!firePressed)
                {
                    continue;
                }

                LocalTransform spaceshipTransform = PredictMovedTransform(
                    transformComponent.ValueRO,
                    movementComponent.ValueRO,
                    deltaTime
                );

                LocalTransform muzzleTransform = spaceshipTransform.TransformTransform(turret.MuzzleLocation);

                // 탄퍼짐
                if (turret.Spread > 0.0F)
                {
                    float halfSpread = turret.Spread * 0.5F;
                    muzzleTransform = muzzleTransform.RotateZ(random.NextFloat(-halfSpread, halfSpread));
                }

                Entity projectile = commandBuffer.Instantiate(turret.ProjectilePrefab);
                commandBuffer.SetComponent(projectile, muzzleTransform);

                turret.FireCooldown = turret.MaxFireCooldown;
            }

            // TODO: 부스터
            bool boostRaw = _boost.ReadValue<float>() > 0.0F;
        }

        /// <summary>
        /// 이번 프레임의 이동(<see cref="SpaceshipMovementSystem"/>)이 적용된 뒤의
        /// Transform을 미리 계산해서 반환한다. 계산식은 <see cref="MoveJob"/>과 동일하다.
        /// </summary>
        private static LocalTransform PredictMovedTransform(
            in LocalTransform current,
            in SpaceshipMovementComponent movement,
            float deltaTime
        )
        {
            float length = math.length(movement.Forward);

            // Forward가 0벡터면 이동/회전 방향을 알 수 없으므로 현재 Transform을 그대로 사용.
            if (length <= math.EPSILON)
            {
                return current;
            }

            float2 velocity = movement.Forward * (movement.Speed / length);

            return LocalTransform.FromPositionRotationScale(
                new float3(current.Position.xy + velocity * deltaTime, current.Position.z),
                quaternion.RotateZ(-math.atan2(movement.Forward.x, movement.Forward.y)),
                current.Scale
            );
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