using UnityEngine;

namespace OOPSample
{
    /// <summary>
    /// ECS 샘플의 <c>SimulationRandom</c>(= <c>Unity.Mathematics.Random</c>) 대응.
    ///
    /// <c>UnityEngine.Random</c>은 전역 상태를 공유하기 때문에 다른 코드가 끼어들면 시퀀스가
    /// 달라집니다. 스폰 분포를 재현 가능하게 유지하려고 xorshift32를 직접 들고 다닙니다.
    /// (ECS 버전과 난수 시퀀스 자체가 같지는 않지만, 분포와 결정성은 동일합니다.)
    /// </summary>
    public struct SimulationRandom
    {
        private uint m_State;

        public SimulationRandom(uint seed)
        {
            m_State = seed == 0u ? 1u : seed;
        }

        public uint NextUInt()
        {
            m_State ^= m_State << 13;
            m_State ^= m_State >> 17;
            m_State ^= m_State << 5;
            return m_State;
        }

        /// <summary>[0, 1) 구간의 균등 분포.</summary>
        public float NextFloat() => (NextUInt() >> 8) * (1f / 16777216f);

        public float NextFloat(float min, float max) => min + NextFloat() * (max - min);

        public Vector2 NextFloat2(Vector2 min, Vector2 max)
            => new Vector2(NextFloat(min.x, max.x), NextFloat(min.y, max.y));
    }
}
