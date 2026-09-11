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
    }
}