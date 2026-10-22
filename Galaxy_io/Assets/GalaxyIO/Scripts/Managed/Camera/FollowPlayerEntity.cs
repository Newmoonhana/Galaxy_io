using GalaxyIO.DOTS.Client.Components.Singleton;
using GalaxyIO.DOTS.Components.Actor;
using GalaxyIO.DOTS.Spaceship;
using GalaxyIO.Tools;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.NetCode;
using Unity.Transforms;
using UnityEngine;
using UnityEngine.InputSystem;

namespace GalaxyIO.Camera
{
    /// <summary>
    /// DOTS 오브젝트인 플레이어 우주선을 따라다니는 카메라 로직.
    /// 우주선을 화면 중앙에 고정하지 않고 진행 방향 쪽으로 카메라를 밀어 시야를 확보함.
    /// 마우스 휠로 시야 크기를 조절함. 축소 한계는 우주선마다의 최대 시야 크기를 따름.
    /// 시점 이동 키를 누르는 동안 커서 쪽으로 카메라를 밀어냄.
    /// </summary>
    public sealed class FollowPlayerEntity : MonoBehaviour
    {
        [SerializeField]
        [Tooltip("최대 속력일 때 카메라가 기수 방향으로 앞서 나가는 거리")]
        [Min(0.0F)]
        private float lookAheadDistance = 5F;

        [SerializeField]
        [Tooltip("시야 방향이 목표 방향으로 전환되는 속도. 클수록 즉각적으로 반응한다.")]
        [Min(0.0F)]
        private float smoothSpeed = 10F;

        [SerializeField]
        [Tooltip("최대로 확대했을 때의 시야 크기 비율. 우주선의 최대 시야 크기(SpaceshipAuthoring.MaxOrthographicSize) 기준.")]
        [Range(0.05F, 1.0F)]
        private float minZoomRatio = 0.5333F;

        [SerializeField]
        [Tooltip("휠 한 칸당 시야 크기 변화 비율")]
        [Range(0.01F, 0.5F)]
        private float zoomStep = 0.1F;

        [SerializeField]
        [Tooltip("시야 크기가 목표 크기로 전환되는 속도. 클수록 즉각적으로 반응한다.")]
        [Min(0.0F)]
        private float zoomSmoothSpeed = 10F;

        [SerializeField]
        [Tooltip("시점 이동 (마우스 휠 누르기). 누르는 동안 커서 쪽으로 카메라를 밀어냄.")]
        private InputActionReference shiftAction;

        [SerializeField]
        [Tooltip("우주선이 화면 가장자리 쪽으로 밀려날 수 있는 최대 비율. 0.6 이면 화면을 8:2 로 나누는 자리까지.")]
        [Range(0.0F, 1.0F)]
        private float maxShift = 0.6F;

        private EntityManager entityManager;
        private EntityQuery playerQuery;
        private EntityQuery stateQuery;

        private UnityEngine.Camera targetCamera;

        // 탄 우주선의 최대 시야 크기. 우주선이 없으면 마지막 값을 유지함.
        private float maxZoom;

        // 휠로 정한 최대 시야 크기 대비 비율. 우주선을 바꿔도 휠 단계가 그대로 유지됨.
        private float zoomRatio = 1F;

        // 현재 적용 중인 오프셋. 목표 오프셋을 향해 매 프레임 수렴함.
        private Vector2 currentLookAhead;
        private Vector2 currentShift;

        private void Awake()
        {
            // 서버에서 비활성화함
            if (Application.isBatchMode)
            {
                gameObject.SetActive(false);
            }
        }

        // 캐시한 쿼리가 속한 클라이언트 월드. 세션 종료 시 Dispose 되므로 매 프레임 확인함.
        private World clientWorld;

        private void Start()
        {
            clientWorld = ClientServerBootstrap.ClientWorld;

            if (clientWorld == null)
            {
                enabled = false;
                return;
            }

            entityManager = clientWorld.EntityManager;
            playerQuery = entityManager.CreateEntityQuery(
                ComponentType.ReadOnly<GhostOwnerIsLocal>(),
                ComponentType.ReadOnly<LocalTransform>(),
                ComponentType.ReadOnly<PlayerSpaceshipMovementComponent>(),
                ComponentType.ReadOnly<SpaceshipCameraComponent>()
            );
            stateQuery = entityManager.CreateEntityQuery(ComponentType.ReadOnly<PlayerStateComponent>());

            targetCamera = GetComponent<UnityEngine.Camera>();
            maxZoom = targetCamera.orthographicSize;
        }

        private void OnEnable()
        {
            shiftAction?.action.Enable();
        }

        private void OnDisable()
        {
            shiftAction?.action.Disable();
        }

        private void LateUpdate()
        {
            // 세션을 나가면 클라이언트 월드가 Dispose 됨. 캐시한 쿼리를 더 쓰면 안 됨.
            if (!ClientWorldScope.IsAlive(clientWorld))
            {
                enabled = false;
                return;
            }

            // 메뉴, 인벤토리가 열린 동안엔 휠을 UI 가, 커서가 레이더 위에 있으면 레이더가 가짐.
            bool menuOpen = stateQuery.TryGetSingleton(out PlayerStateComponent playerState)
                            && (playerState.GameMenuOpened || playerState.InventoryOpened);

            // 화면 가운데에서 커서까지를 화면 절반 크기로 나눈 값(-1 ~ 1). 시점 이동 키를 누르는 동안만.
            Vector2 shift = Vector2.zero;

            if (Mouse.current != null && Application.isFocused && !menuOpen)
            {
                float scroll = Mouse.current.scroll.ReadValue().y;

                // 휠 위 = 확대. 비율로 바꿔 어느 배율에서도 한 칸의 체감이 같음.
                if (scroll != 0.0F && !playerState.RadarHovered)
                {
                    zoomRatio = Mathf.Clamp(zoomRatio * (1F - Mathf.Sign(scroll) * zoomStep), minZoomRatio, 1F);
                }

                if (shiftAction != null && shiftAction.action.IsPressed() && !playerState.ChatFocused)
                {
                    Vector2 half = new Vector2(Screen.width, Screen.height) * 0.5F;
                    Vector2 cursor = (Mouse.current.position.ReadValue() - half) / half;
                    shift = new Vector2(Mathf.Clamp(cursor.x, -1F, 1F), Mathf.Clamp(cursor.y, -1F, 1F));
                }
            }

            Entity player = Entity.Null;

            if (!playerQuery.IsEmpty)
            {
                using var players = playerQuery.ToEntityArray(Allocator.Temp);
                player = players[0];
                maxZoom = entityManager.GetComponentData<SpaceshipCameraComponent>(player).MaxOrthographicSize;
            }

            // 우주선을 바꾸면 새 최대 시야 크기로 보간됨.
            targetCamera.orthographicSize = Mathf.Lerp(
                targetCamera.orthographicSize,
                maxZoom * zoomRatio,
                1F - Mathf.Exp(-zoomSmoothSpeed * Time.deltaTime)
            );

            if (player == Entity.Null)
            {
                return;
            }

            LocalTransform playerTransform = entityManager.GetComponentData<LocalTransform>(player);
            PlayerSpaceshipMovementComponent movement =
                entityManager.GetComponentData<PlayerSpaceshipMovementComponent>(player);

            // 오프셋만 보간함. 프레임률과 무관한 지수 감쇠.
            currentLookAhead = Vector2.Lerp(
                currentLookAhead,
                GetLookAhead(playerTransform, movement),
                1F - Mathf.Exp(-smoothSpeed * Time.deltaTime)
            );

            currentShift = Vector2.Lerp(currentShift, shift, 1F - Mathf.Exp(-smoothSpeed * Time.deltaTime));

            // 화면 절반 크기(월드 단위). 우주선이 이 비율 넘게 가장자리로 밀려나지 않게 합친 오프셋을 자름.
            Vector2 extents = new Vector2(targetCamera.orthographicSize * targetCamera.aspect, targetCamera.orthographicSize) * maxShift;
            Vector2 offset = currentLookAhead + Vector2.Scale(currentShift, extents);

            offset = new Vector2(Mathf.Clamp(offset.x, -extents.x, extents.x), Mathf.Clamp(offset.y, -extents.y, extents.y));

            // 우주선의 위치는 보간 없이 그대로 따라가므로 화면상 위치가 흔들리지 않음.
            transform.position = new Vector3
            (
                playerTransform.Position.x + offset.x,
                playerTransform.Position.y + offset.y,
                transform.position.z
            );
        }

        /// <summary>
        /// 카메라를 우주선의 기수 방향으로 밀어낼 오프셋(월드 단위)을 계산함.
        /// 정지 상태면 0, 최대 속력이면 <see cref="lookAheadDistance"/>가 됨.
        /// </summary>
        private Vector2 GetLookAhead(
            in LocalTransform transform,
            in PlayerSpaceshipMovementComponent movement)
        {
            if (movement.MaxSpeed <= math.EPSILON)
            {
                return Vector2.zero;
            }

            // 기수 방향 * (기수 방향 속력 / 최대 속력). 가속/감속을 따라 오프셋도 늘고 줆.
            float2 forward = math.normalizesafe(SpaceshipHeading.FromRotation(transform.Rotation), float2.zero);
            float speedRatio = math.saturate(math.dot(movement.Velocity, forward) / movement.MaxSpeed);

            return new Vector2(forward.x, forward.y) * (speedRatio * lookAheadDistance);
        }
    }
}