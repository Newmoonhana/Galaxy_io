using GalaxyIO.DOTS.Components;
using GalaxyIO.DOTS.Spaceship;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.NetCode;
using Unity.Transforms;
using UnityEngine;

namespace GalaxyIO.Camera
{
    /// <summary>
    /// DOTS 오브젝트인 플레이어 우주선을 따라다니는 카메라 로직.
    /// 우주선을 화면 중앙에 고정하지 않고 진행 방향 쪽으로 카메라를 밀어 시야를 확보함.
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

        private EntityManager entityManager;
        private EntityQuery playerQuery;

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

        private void Start()
        {
            World clientWorld = ClientServerBootstrap.ClientWorld;

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
        }

        private void LateUpdate()
        {
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

            // 기수 방향 * (현재 속력 / 최대 속력). 가속/감속을 따라 오프셋도 늘고 줆.
            float2 forward = math.normalizesafe(SpaceshipHeading.FromRotation(transform.Rotation), float2.zero);
            float speedRatio = math.saturate(movement.Speed / movement.MaxSpeed);

            return new Vector2(forward.x, forward.y) * (speedRatio * lookAheadDistance);
        }
    }
}