using GalaxyIO.Components.Tags;
using Unity.Entities;
using Unity.Transforms;
using UnityEngine;
using UnityEngine.InputSystem;

namespace GalaxyIO.Camera
{
    /// <summary>
    /// DOTS 오브젝트인 플레이어 우주선을 따라다니는 카메라 로직.
    /// <p/>
    /// 우주선을 화면 중앙에 고정하지 않고 진행 방향 쪽으로 카메라를 밀어 시야를 확보한다.
    /// WASD 이동 중에는 입력 방향으로, 그 외에는 화면 중심에서 마우스 커서 방향으로 밀어낸다.
    /// <p/>
    /// 보간은 카메라 위치가 아니라 '밀어내는 오프셋'에만 적용한다. 위치를 보간하면
    /// 카메라가 우주선에 끌려다니며 뒤처지지만, 오프셋만 보간하면 우주선은 화면에
    /// 고정된 채로 시야 방향만 부드럽게 전환된다. (WASD 입력의 급격한 방향 전환 대응)
    /// </summary>
    public sealed class FollowPlayerEntity : MonoBehaviour
    {
        [SerializeField]
        [Tooltip("PlayerControlAuthoring에 지정한 것과 같은 InputActionAsset")]
        private InputActionAsset inputActionAsset;

        [SerializeField]
        [Tooltip("카메라가 진행 방향으로 앞서 나가는 거리 (WASD 이동 중이거나 커서가 화면 가장자리일 때 최대)")]
        [Min(0.0F)]
        private float lookAheadDistance = 5F;

        [SerializeField]
        [Tooltip("시야 방향이 목표 방향으로 전환되는 속도. 클수록 즉각적으로 반응한다.")]
        [Min(0.0F)]
        private float smoothSpeed = 10F;

        private EntityManager entityManager;
        private EntityQuery playerQuery;

        // `inputActionAsset`에서 찾아온 WASD 이동 Action.
        private InputAction wasdMove;

        // Action을 이 컴포넌트가 직접 활성화했는지 여부.
        // `PlayerInputSystem`이 이미 켜 둔 Action을 OnDisable에서 꺼 버리면
        // 우주선 조작이 죽으므로, 직접 켠 경우에만 되돌린다.
        private bool wasdMoveEnabledBySelf;

        // 현재 적용 중인 오프셋. 목표 오프셋을 향해 매 프레임 수렴한다.
        private Vector2 currentLookAhead;

        private void Start()
        {
            entityManager = World.DefaultGameObjectInjectionWorld.EntityManager;

            playerQuery = entityManager.CreateEntityQuery(
                ComponentType.ReadOnly<LocalPlayerTag>(),
                ComponentType.ReadOnly<LocalTransform>()
            );
        }

        private void OnEnable()
        {
            if (inputActionAsset == null)
            {
                Debug.LogError(
                    $"{nameof(FollowPlayerEntity)}에 {nameof(InputActionAsset)}이 지정되지 않아 " +
                    "WASD 방향 시야 확보가 비활성화됩니다. (마우스 커서 기준으로만 동작)",
                    this
                );

                return;
            }

            // 이름이 바뀌면 조용히 커서 전용으로 되돌아가지 않고 즉시 에러로 드러나도록 한다.
            wasdMove = inputActionAsset
                .FindActionMap("Gameplay", throwIfNotFound: true)
                .FindAction("Move_By_Buttons", throwIfNotFound: true);

            // `PlayerInputSystem`의 활성화 여부에 의존하지 않도록 직접 활성화한다.
            // (비활성 Action의 ReadValue는 항상 0을 반환한다.)
            if (!wasdMove.enabled)
            {
                wasdMove.Enable();
                wasdMoveEnabledBySelf = true;
            }
        }

        private void OnDisable()
        {
            if (wasdMoveEnabledBySelf)
            {
                wasdMove.Disable();
                wasdMoveEnabledBySelf = false;
            }

            wasdMove = null;
        }

        private void LateUpdate()
        {
            if (playerQuery.IsEmpty)
            {
                return;
            }

            Entity player = playerQuery.GetSingletonEntity();
            LocalTransform playerTransform = entityManager.GetComponentData<LocalTransform>(player);

            // 오프셋만 보간한다. 프레임률과 무관한 지수 감쇠.
            currentLookAhead = Vector2.Lerp(
                currentLookAhead,
                GetLookAhead(),
                1F - Mathf.Exp(-smoothSpeed * Time.deltaTime)
            );

            // 우주선의 위치 자체는 보간 없이 그대로 따라가므로 화면상 위치가 흔들리지 않는다.
            transform.position = new Vector3
            (
                playerTransform.Position.x + currentLookAhead.x,
                playerTransform.Position.y + currentLookAhead.y,
                transform.position.z
            );
        }

        /// <summary>
        /// 카메라를 우주선의 진행 방향 쪽으로 밀어낼 오프셋(월드 단위)을 계산한다.
        /// </summary>
        private Vector2 GetLookAhead()
        {
            Vector2 wasdValue = wasdMove != null ? wasdMove.ReadValue<Vector2>() : Vector2.zero;

            // WASD 이동은 항상 최대 속력이므로(`PlayerInputSystem` 참고) 세기를 따질 필요 없이
            // 입력 방향으로 lookAheadDistance만큼 밀어낸다. 대각 입력의 길이가 √2가 되지
            // 않도록 정규화한다.
            if (wasdValue != Vector2.zero)
            {
                return wasdValue.normalized * lookAheadDistance;
            }

            return GetCursorLookAhead();
        }

        /// <summary>
        /// `화면 중심 -> 마우스 커서` 방향으로 카메라를 밀어낼 오프셋(월드 단위)을 계산한다.
        /// 커서가 화면 중심이면 0, 가장자리면 <see cref="lookAheadDistance"/>가 된다.
        /// </summary>
        private Vector2 GetCursorLookAhead()
        {
            Mouse mouse = Mouse.current;
            Vector2 screenCenter = new Vector2(Screen.width, Screen.height) * 0.5F;

            // 마우스가 없는 환경이거나 화면 크기를 알 수 없으면 우주선을 화면 중앙에 둔다.
            if (mouse == null || screenCenter.x <= 0.0F || screenCenter.y <= 0.0F)
            {
                return Vector2.zero;
            }

            // 화면 비율과 해상도에 무관하도록 축별로 정규화한다. (가장자리에서 1)
            Vector2 cursorPos = mouse.position.ReadValue();
            Vector2 offset = new Vector2(
                (cursorPos.x - screenCenter.x) / screenCenter.x,
                (cursorPos.y - screenCenter.y) / screenCenter.y
            );

            // 모서리(길이 √2)나 창 밖으로 커서가 나간 경우까지 과하게 밀리지 않도록 제한.
            if (offset.sqrMagnitude > 1.0F)
            {
                offset.Normalize();
            }

            return offset * lookAheadDistance;
        }
    }
}
