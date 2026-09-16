using GalaxyIO.DOTS.Components.Actor;
using Unity.Burst;
using Unity.Mathematics;

namespace GalaxyIO.DOTS.Collision
{
    /// <summary>
    /// 축 정렬 경계 상자.
    /// </summary>
    public struct Aabb
    {
        public float2 Min;
        public float2 Max;

        public static Aabb FromCenterHalf(float2 center, float2 half)
        {
            return new Aabb { Min = center - half, Max = center + half };
        }

        public Aabb Expand(float margin)
        {
            return new Aabb { Min = Min - margin, Max = Max + margin };
        }

        public bool Overlaps(in Aabb other)
        {
            return math.all(Min <= other.Max) && math.all(Max >= other.Min);
        }
    }

    /// <summary>
    /// 충돌체의 경계 상자 계산.
    /// </summary>
    [BurstCompile]
    public static class ColliderBounds
    {
        public static Aabb ToAabb(in ColliderWorld collider)
        {
            switch (collider.Shape)
            {
                case ColliderShape.Obb:
                    // 회전한 사각형은 두 축 투영의 합이 반너비/반높이가 됨.
                    return Aabb.FromCenterHalf(
                        collider.Center,
                        math.abs(collider.AxisX) * collider.Extents.x
                        + math.abs(collider.AxisY) * collider.Extents.y
                    );

                case ColliderShape.Sector:
                    return SectorAabb(collider);

                default:
                    return Aabb.FromCenterHalf(collider.Center, new float2(collider.Extents.x));
            }
        }

        /// <summary>
        /// 부채꼴은 꼭짓점, 양 끝점, 그리고 각도 안에 든 사분면 끝점이 경계를 정함.
        /// </summary>
        private static Aabb SectorAabb(in ColliderWorld sector)
        {
            float radius = sector.Extents.x;
            float halfAngle = sector.Extents.y;

            // 부채꼴은 로컬 +Y(AxisY)를 향함. 꼭짓점 기준 상대 좌표로 구한 뒤 옮김.
            float2 left = Rotate(sector.AxisY, halfAngle) * radius;
            float2 right = Rotate(sector.AxisY, -halfAngle) * radius;

            var bounds = new Aabb
            {
                Min = math.min(float2.zero, math.min(left, right)),
                Max = math.max(float2.zero, math.max(left, right)),
            };

            float cosHalf = math.cos(halfAngle);

            for (int i = 0; i < 4; i++)
            {
                float2 axis = Cardinal(i);

                if (math.dot(axis, sector.AxisY) < cosHalf)
                {
                    continue;
                }

                bounds.Min = math.min(bounds.Min, axis * radius);
                bounds.Max = math.max(bounds.Max, axis * radius);
            }

            bounds.Min += sector.Center;
            bounds.Max += sector.Center;

            return bounds;
        }

        private static float2 Rotate(float2 vector, float angle)
        {
            math.sincos(angle, out float sin, out float cos);

            return new float2(vector.x * cos - vector.y * sin, vector.x * sin + vector.y * cos);
        }

        private static float2 Cardinal(int index)
        {
            switch (index)
            {
                case 0:  return new float2(1.0F, 0.0F);
                case 1:  return new float2(0.0F, 1.0F);
                case 2:  return new float2(-1.0F, 0.0F);
                default: return new float2(0.0F, -1.0F);
            }
        }
    }
}
