using Unity.Mathematics;

namespace GalaxyIO.DOTS.Spaceship
{
    /// <summary>
    /// 기수와 무관한 방향으로 움직이는 호버링 속도의 가감속 헬퍼.
    /// </summary>
    public static class SpaceshipHovering
    {
        // 현재 속도와 목표 속도의 방향이 이보다 어긋나면(cos) 방향 전환으로 봄.
        private const float TurnThreshold = 0.99F;

        /// <summary>
        /// 목표 속도까지 한 틱의 가감속 한계 안에서 접근함. 목표가 (0, 0)이면 감속도를 씀.
        /// 움직이던 중에 다른 방향으로 바꾸면(turning) 목표 속도에 닿을 때까지 가감속 대신 maneuver 를 씀.
        /// 벡터를 보간하므로 방향 전환도 한계를 지나 한 프레임에 뒤집히지 않음.
        /// </summary>
        public static float2 Approach(
            float2 current,
            float2 desired,
            float acceleration,
            float deceleration,
            float maneuver,
            ref bool turning,
            float deltaTime)
        {
            float2 delta = desired - current;
            float deltaLength = math.length(delta);

            bool driving = math.lengthsq(desired) > math.EPSILON;

            // 손을 떼면 방향 전환도 끝남.
            if (!driving)
            {
                turning = false;
            }
            else if (math.lengthsq(current) > math.EPSILON
                     && math.dot(math.normalize(current), math.normalize(desired)) < TurnThreshold)
            {
                turning = true;
            }

            float maxDelta = deltaTime * (turning ? maneuver : driving ? acceleration : deceleration);

            if (deltaLength > maxDelta)
            {
                return current + delta * (maxDelta / deltaLength);
            }

            turning = false;
            return desired;
        }
    }
}