using UnityEngine;

namespace OOPSample
{
    /// <summary>
    /// 봇 한 마리. ECS 샘플의 <c>BotTag</c> + <c>Velocity</c> + <c>BoxSize</c> +
    /// <c>BotMovementSystem</c> + <c>CollisionDetectionSystem</c>을 전부 합친 것입니다.
    ///
    /// 이것이 두 구현의 핵심적인 차이입니다. DOTS 쪽에서는 데이터가 청크에 연속으로 깔리고
    /// 로직이 Burst 잡으로 모든 청크를 병렬 순회하지만, 여기서는 개체 하나하나가 자기 데이터를
    /// 들고 자기 <see cref="Update"/>를 호출받습니다. 봇이 10만 마리면 매니지드 Update 호출이
    /// 10만 번, Transform 쓰기가 10만 번 일어납니다. 성능 비교에서 재고 싶은 것이 바로 이 비용이므로,
    /// 여기서 봇 이동을 매니저 루프로 접어 넣는 "최적화"는 일부러 하지 않았습니다.
    ///
    /// 다만 다음 두 가지는 아키텍처와 무관한, 어떤 OOP 코드베이스에서도 하는 캐싱이라 적용했습니다.
    /// 이걸 빼면 ECS와의 비교가 아니라 Unity API 함정과의 비교가 되어 버립니다.
    ///   * <c>transform</c> 프로퍼티를 Awake에서 한 번만 받아 둔다.
    ///   * <c>Time.deltaTime</c>은 <see cref="SimulationRunner"/>가 프레임당 한 번 읽어
    ///     <see cref="SimulationTime.DeltaTime"/>에 게시한 값을 읽는다.
    /// </summary>
    [AddComponentMenu("OOP Simulator/Bot")]
    [DisallowMultipleComponent]
    public sealed class BotBehaviour : MonoBehaviour
    {
        private Transform m_Transform;

        private SimulationEventBus m_Bus;
        private PlayerBehaviour m_Player;

        private Vector2 m_Position;
        private Vector2 m_Velocity;

        // 벽 반사에 쓰는, 박스 반쪽 크기만큼 안쪽으로 줄인 경계. 스폰 시 한 번 계산합니다.
        private Vector2 m_Min;
        private Vector2 m_Max;

        private float m_HalfExtent;

        /// <summary><see cref="BotRegistry"/>가 O(1) 제거를 하려고 심어 두는 인덱스.</summary>
        public int RegistryIndex { get; internal set; } = -1;

        /// <summary>
        /// ECS의 <c>EntityManager.Exists</c> 대응. <c>Destroy</c>는 프레임 끝에야 실제로
        /// 반영되므로, 이미 처리하기로 결정된 봇을 표시해 둘 플래그가 따로 필요합니다.
        /// </summary>
        public bool IsAlive { get; private set; }

        public Vector2 Position => m_Position;

        private void Awake()
        {
            m_Transform = transform;

            // Initialize가 의존성을 넣어 주기 전에는 Update가 돌면 안 됩니다. 매 프레임 null을
            // 확인하는 대신(그 확인 자체가 Unity의 가짜 null 검사라 공짜가 아닙니다) 컴포넌트를
            // 꺼 둡니다.
            enabled = false;
        }

        /// <summary>
        /// 스포너가 Instantiate 직후 호출합니다. 생성자를 쓸 수 없는 MonoBehaviour에서
        /// 의존성을 주입하는 표준적인 방법이며, 봇이 매 프레임 매니저를 찾아다니지 않아도 되게 합니다.
        /// </summary>
        public void Initialize(
            SimulationEventBus bus,
            PlayerBehaviour player,
            in WorldBounds bounds,
            float size,
            Vector2 position,
            Vector2 velocity)
        {
            m_Bus = bus;
            m_Player = player;

            m_HalfExtent = size * 0.5f;
            m_Min = bounds.Min + new Vector2(m_HalfExtent, m_HalfExtent);
            m_Max = bounds.Max - new Vector2(m_HalfExtent, m_HalfExtent);

            m_Position = position;
            m_Velocity = velocity;

            if (m_Transform == null)
                m_Transform = transform;

            // 메시는 1x1 쿼드이므로 스케일이 곧 월드 크기입니다. ECS의 LocalTransform.Scale과 동일.
            m_Transform.localScale = new Vector3(size, size, 1f);
            m_Transform.localPosition = new Vector3(position.x, position.y, 0f);

            IsAlive = true;
            enabled = true;
        }

        /// <summary>디스폰이 확정된 봇을 표시합니다. 같은 프레임의 중복 처리를 막습니다.</summary>
        public void MarkDestroyed() => IsAlive = false;

        private void Update()
        {
            // ---- 1. 적분 + 벽 반사 (ECS: BotMovementSystem / BotMoveJob) ----
            var deltaTime = SimulationTime.DeltaTime;
            var position = m_Position + m_Velocity * deltaTime;

            if (position.x < m_Min.x)
            {
                position.x = m_Min.x + (m_Min.x - position.x);
                m_Velocity.x = -m_Velocity.x;
            }
            else if (position.x > m_Max.x)
            {
                position.x = m_Max.x - (position.x - m_Max.x);
                m_Velocity.x = -m_Velocity.x;
            }

            if (position.y < m_Min.y)
            {
                position.y = m_Min.y + (m_Min.y - position.y);
                m_Velocity.y = -m_Velocity.y;
            }
            else if (position.y > m_Max.y)
            {
                position.y = m_Max.y - (position.y - m_Max.y);
                m_Velocity.y = -m_Velocity.y;
            }

            position.x = Mathf.Clamp(position.x, m_Min.x, m_Max.x);
            position.y = Mathf.Clamp(position.y, m_Min.y, m_Max.y);

            m_Position = position;
            m_Transform.localPosition = new Vector3(position.x, position.y, 0f);

            // ---- 2. 플레이어와의 AABB 겹침 검사 (ECS: CollisionDetectionSystem) ----
            //
            // 플레이어는 실행 순서상 이미 이번 프레임 이동을 끝냈으므로(PlayerBehaviour의
            // DefaultExecutionOrder 참고), 어느 봇이 먼저 돌든 같은 플레이어 위치를 봅니다.
            //
            // 여기서 직접 Destroy를 부르거나 점수를 올리지 않는다는 점이 중요합니다. 이 코드가
            // 하는 일은 "겹쳤다"고 보고하는 것뿐이고, 그 사실에 무엇을 할지는 컨슈머들이 정합니다.
            var playerCenter = m_Player.Center;
            var combinedHalfExtent = m_Player.HalfExtent + m_HalfExtent;

            if (Mathf.Abs(position.x - playerCenter.x) > combinedHalfExtent ||
                Mathf.Abs(position.y - playerCenter.y) > combinedHalfExtent)
            {
                return;
            }

            m_Bus.BotHitPlayer.Publish(new BotHitPlayerEvent(this, m_Player, position));
        }
    }
}
