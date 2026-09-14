using Unity.Mathematics;

namespace GalaxyIO.DOTS.Spaceship
{
    /// <summary>
    /// 기수와 무관한 방향으로 움직이는 호버링 속도의 가감속 헬퍼.
    /// </summary>
    public static class SpaceshipHovering
    {
        /// <summary>
        /// 목표 속도까지 한 틱의 가감속 한계 안에서 접근함. 목표가 (0, 0)이면 감속도를 씀.
        /// 벡터를 보간하므로 방향 전환도 같은 한계를 지나 한 프레임에 뒤집히지 않음.
        /// </summary>
        public static float2 Approach(
            float2 current,
            float2 desired,
            float acceleration,
            float deceleration,
            float deltaTime)
        {
            float2 delta = desired - current;
            float deltaLength = math.length(delta);

            float maxDelta = deltaTime * (math.lengthsq(desired) > math.EPSILON ? acceleration : deceleration);

            return deltaLength > maxDelta
                ? current + delta * (maxDelta / deltaLength)
                : desired;
        }
    }
}