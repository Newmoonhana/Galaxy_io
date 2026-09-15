using Unity.Mathematics;
using Unity.Transforms;

namespace GalaxyIO.DOTS.Spaceship
{
    /// <summary>
    /// 우주선의 기수 방향과 <see cref="LocalTransform"/> 회전을 변환하는 헬퍼.
    /// </summary>
    public static class SpaceshipHeading
    {
        /// <summary>
        /// Transform 회전에서 기수 방향을 뽑음.
        /// </summary>
        public static float2 FromRotation(in quaternion rotation)
        {
            // 우주선의 기수는 로컬 +Y 축.
            return math.mul(rotation, math.up()).xy;
        }

        /// <summary>
        /// 기수 방향을 Transform 회전으로 바꿈.
        /// </summary>
        public static quaternion ToRotation(float2 forward)
        {
            return quaternion.RotateZ(-math.atan2(forward.x, forward.y));
        }

        /// <summary>
        /// 기수 방향을 목표 방향으로 최대 <paramref name="maxAngle"/>(라디안)만큼 돌림.
        /// 두 방향 모두 단위 벡터여야 함.
        /// </summary>
        public static float2 RotateTowards(float2 current, float2 desired, float maxAngle)
        {
            float deltaAngle = math.atan2(
                current.x * desired.y - current.y * desired.x,
                math.dot(current, desired)
            );

            float appliedAngle = math.clamp(deltaAngle, -maxAngle, maxAngle);
            math.sincos(appliedAngle, out float sin, out float cos);

            return new float2(
                current.x * cos - current.y * sin,
                current.x * sin + current.y * cos
            );
        }
    }
}