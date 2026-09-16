using GalaxyIO.DOTS.Components.Actor;
using Unity.Burst;
using Unity.Entities;
using Unity.Mathematics;

namespace GalaxyIO.DOTS.Collision
{
    /// <summary>
    /// 충돌 직후 속도를 밀려난 방향으로 돌리는 계산.
    /// </summary>
    [BurstCompile]
    public static class CollisionResponse
    {
        /// <summary>
        /// 속도에 더할 값. 상대 쪽으로 파고드는 성분만 밀려난 방향으로 되돌림.
        /// restitution 이 0이면 미끄러지듯 지나가고, 1이면 그만큼 튕겨 나감.
        /// </summary>
        public static float2 Impulse(
            float2 velocity,
            in DynamicBuffer<SpaceshipCollisionElement> messages,
            float restitution)
        {
            float2 impulse = float2.zero;

            for (int i = 0; i < messages.Length; i++)
            {
                float2 normal = messages[i].Message.Normal;

                // 음수면 상대 쪽으로 파고드는 중. 이미 멀어지는 중이면 건드리지 않음.
                float into = math.dot(velocity, normal);

                if (into >= 0.0F)
                {
                    continue;
                }

                impulse += normal * (-into * (1.0F + restitution));
            }

            return impulse;
        }
    }
}
