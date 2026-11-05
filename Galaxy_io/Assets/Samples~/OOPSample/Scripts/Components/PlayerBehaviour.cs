using UnityEngine;

namespace OOPSample
{
    /// <summary>
    /// 플레이어 박스. ECS 샘플의 <c>PlayerTag</c> + <c>PlayerMovementSystem</c> 대응입니다.
    ///
    /// 실행 순서를 봇보다 앞으로 당겨 둔 것이 핵심입니다. DOTS에서는 플레이어와 봇의 이동이
    /// 모두 <c>MovementSystemGroup</c> 안에서 끝난 뒤에야 <c>CollisionSystemGroup</c>이 돌기
    /// 때문에, 충돌 검사는 항상 "이번 프레임 이동을 마친 플레이어"를 봅니다. 봇이 자기 Update
    /// 안에서 충돌까지 처리하는 OOP 구조에서 같은 순서를 보장하려면, 플레이어가 모든 봇보다
    /// 먼저 움직여야 합니다.
    /// </summary>
    [AddComponentMenu("OOP Simulator/Player")]
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(-500)]
    public sealed class PlayerBehaviour : MonoBehaviour
    {
        private Transform m_Transform;

        private PlayerInputReader m_Input;

        private Vector2 m_Position;
        private Vector2 m_Velocity;

        private Vector2 m_Min;
        private Vector2 m_Max;

        private float m_HalfExtent;
        private float m_Speed;

        /// <summary>봇의 충돌 검사가 읽어 가는 값. Transform을 되읽지 않도록 필드로 들고 있습니다.</summary>
        public Vector2 Center => m_Position;

        public float HalfExtent => m_HalfExtent;

        private void Awake()
        {
            m_Transform = transform;
            enabled = false;
        }

        public void Initialize(PlayerInputReader input, in WorldBounds bounds, float size, float speed)
        {
            m_Input = input;
            m_Speed = speed;

            m_HalfExtent = size * 0.5f;
            m_Min = bounds.Min + new Vector2(m_HalfExtent, m_HalfExtent);
            m_Max = bounds.Max - new Vector2(m_HalfExtent, m_HalfExtent);

            if (m_Transform == null)
                m_Transform = transform;

            m_Position = Vector2.zero;
            m_Velocity = Vector2.zero;

            m_Transform.localScale = new Vector3(size, size, 1f);
            m_Transform.localPosition = Vector3.zero;

            enabled = true;
        }

        private void Update()
        {
            var move = m_Input.Move;
            var direction = move.sqrMagnitude > 1e-6f ? move.normalized : Vector2.zero;

            m_Velocity = direction * m_Speed;

            var position = m_Position + m_Velocity * SimulationTime.DeltaTime;
            position.x = Mathf.Clamp(position.x, m_Min.x, m_Max.x);
            position.y = Mathf.Clamp(position.y, m_Min.y, m_Max.y);

            m_Position = position;
            m_Transform.localPosition = new Vector3(position.x, position.y, 0f);
        }
    }
}
