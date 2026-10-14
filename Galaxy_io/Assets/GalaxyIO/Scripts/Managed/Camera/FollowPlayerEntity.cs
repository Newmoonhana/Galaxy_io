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
    /// 마우스 휠로 시야 크기를 조절함.
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
        [Tooltip("최대로 확대했을 때의 시야 크기(Orthographic Size). 축소는 카메라의 기본 크기까지만 됨.")]
        [Min(0.1F)]
        private float minZoom = 8F;

        [SerializeField]
        [Tooltip("휠 한 칸당 시야 크기 변화 비율")]
        [Range(0.01F, 0.5F)]
        private float zoomStep = 0.1F;

        [SerializeField]
        [Tooltip("시야 크기가 목표 크기로 전환되는 속도. 클수록 즉각적으로 반응한다.")]
        [Min(0.0F)]
        private float zoomSmoothSpeed = 10F;

        private EntityManager entityManager;
        private EntityQuery playerQuery;
        private EntityQuery stateQuery;

        private UnityEngine.Camera targetCamera;

        // 카메라의 기본 시야 크기. 이보다 축소하지 않음.
        private float maxZoom;

        // 휠로 정한 목표 시야 크기. 실제 크기는 매 프레임 수렴함.
        private float targetZoom;

        // 현재 적용 중인 오프셋. 목표 오프셋을 향해 매 프레임 수렴함.
        private Vector2 currentLookAhead;

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
                ComponentType.ReadOnly<PlayerSpaceshipMovementComponent>()
            );
            stateQuery = entityManager.CreateEntityQuery(ComponentType.ReadOnly<PlayerStateComponent>());

            targetCamera = GetComponent<UnityEngine.Camera>();
            maxZoom = targetCamera.orthographicSize;
            targetZoom = maxZoom;
        }

        private void LateUpdate()
        {
            // 세션을 나가면 클라이언트 월드가 Dispose 됨. 캐시한 쿼리를 더 쓰면 안 됨.
            if (!ClientWorldScope.IsAlive(clientWorld))
            {
                enabled = false;
                return;
            }

            // 메뉴, 인벤토리가 열린 동안엔 휠을 UI 가 가짐.
            bool menuOpen = stateQuery.TryGetSingleton(out PlayerStateComponent playerState)
                            && (playerState.GameMenuOpened || playerState.InventoryOpened);

            if (Mouse.current != null && Application.isFocused && !menuOpen)
            {
                float scroll = Mouse.current.scroll.ReadValue().y;

                // 휠 위 = 확대. 비율로 바꿔 어느 배율에서도 한 칸의 체감이 같음.
                if (scroll != 0.0F)
                {
                    targetZoom = Mathf.Clamp(
                        targetZoom * (1F - Mathf.Sign(scroll) * zoomStep),
                        Mathf.Min(minZoom, maxZoom),
                        maxZoom
                    );
                }
            }

            targetCamera.orthographicSize = Mathf.Lerp(
                targetCamera.orthographicSize,
                targetZoom,
                1F - Mathf.Exp(-zoomSmoothSpeed * Time.deltaTime)
            );

            if (playerQuery.IsEmpty)
            {
                return;
            }

            using var players = playerQuery.ToEntityArray(Allocator.Temp);
            Entity player = players[0];
            LocalTransform playerTransform = entityManager.GetComponentData<LocalTransform>(player);
            PlayerSpaceshipMovementComponent movement =
                entityManager.GetComponentData<PlayerSpaceshipMovementComponent>(player);

            // 오프셋만 보간함. 프레임률과 무관한 지수 감쇠.
            currentLookAhead = Vector2.Lerp(
                currentLookAhead,
                GetLookAhead(playerTransform, movement),
                1F - Mathf.Exp(-smoothSpeed * Time.deltaTime)
            );

            // 우주선의 위치는 보간 없이 그대로 따라가므로 화면상 위치가 흔들리지 않음.
            transform.position = new Vector3
            (
                playerTransform.Position.x + currentLookAhead.x,
                playerTransform.Position.y + currentLookAhead.y,
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