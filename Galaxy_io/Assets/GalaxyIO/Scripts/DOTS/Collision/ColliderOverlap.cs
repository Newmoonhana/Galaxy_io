using GalaxyIO.DOTS.Components.Collision;
using Unity.Burst;
using Unity.Mathematics;
using Unity.Transforms;

namespace GalaxyIO.DOTS.Collision
{
    /// <summary>
    /// 월드 좌표로 펼친 충돌체. <see cref="ColliderComponent"/>와 Transform을 합성한 결과.
    /// </summary>
    public struct ColliderWorld
    {
        public ColliderShape Shape;

        // 월드 중심
        public float2 Center;

        // 충돌체의 회전 축(단위 벡터). Circle은 사용하지 않음.
        public float2 AxisX;
        public float2 AxisY;

        // Obb: (반너비, 반높이). Circle: x가 반지름.
        public float2 Extents;
    }

    /// <summary>
    /// 겹침의 모양. 겹치지 않았으면 채워지지 않음.
    /// </summary>
    public struct ColliderHit
    {
        // 앞쪽 충돌체를 뒤쪽에서 떼어내는 방향(단위 벡터).
        public float2 Normal;

        // 겹친 깊이. Normal 방향으로 이만큼 옮기면 딱 맞닿음.
        public float Depth;
    }

    /// <summary>
    /// 충돌체 쌍의 겹침 판정 모음.
    /// </summary>
    [BurstCompile]
    public static class ColliderOverlap
    {
        /// <summary>
        /// 로컬 충돌체를 Transform에 실어 월드 좌표로 펼침.
        /// </summary>
        public static ColliderWorld ToWorld(in ColliderComponent collider, in LocalTransform transform)
        {
            // LocalTransform.Scale은 균등 스케일이라 회전축은 단위 벡터로 유지됨.
            float2 axisX = math.rotate(transform.Rotation, new float3(1.0F, 0.0F, 0.0F)).xy;
            float2 axisY = new float2(-axisX.y, axisX.x);

            return new ColliderWorld
            {
                Shape   = collider.Shape,
                Center  = transform.Position.xy
                          + (axisX * collider.Center.x + axisY * collider.Center.y) * transform.Scale,
                AxisX   = axisX,
                AxisY   = axisY,
                Extents = collider.Extents * transform.Scale,
            };
        }

        /// <summary>
        /// 충돌체를 변위만큼 늘려 한 틱 동안 지나간 영역을 감쌈.
        /// 틱 이동량이 충돌체보다 큰 고속 물체가 표적을 통과하는 것(터널링) 방지.
        /// </summary>
        public static ColliderWorld Sweep(in ColliderWorld collider, float2 displacement)
        {
            ColliderWorld swept = collider;

            // 중심을 이동 경로의 중점으로 옮기고 반경을 절반씩 키움.
            swept.Center = collider.Center - displacement * 0.5F;

            swept.Extents = collider.Shape == ColliderShape.Circle
                // 원: 반지름 + 이동거리 절반이면 스윕한 캡슐을 정확히 포함함.
                ? collider.Extents + math.length(displacement) * 0.5F
                // Obb: 변위를 로컬 축에 투영한 만큼만 키움.
                : collider.Extents + math.abs(new float2(
                      math.dot(displacement, collider.AxisX),
                      math.dot(displacement, collider.AxisY)
                  )) * 0.5F;

            return swept;
        }

        /// <summary>
        /// 두 충돌체가 겹치는지 판정함. 셰이프 조합은 순서를 정규화해 처리함.
        /// <paramref name="hit"/>의 방향은 <paramref name="a"/>를 떼어내는 쪽임.
        /// </summary>
        public static bool Test(in ColliderWorld a, in ColliderWorld b, out ColliderHit hit)
        {
            // (작은 enum, 큰 enum) 순으로 맞춤. 대칭 조합을 한 번만 구현하면 됨.
            bool flip = a.Shape > b.Shape;
            ColliderWorld lhs = flip ? b : a;
            ColliderWorld rhs = flip ? a : b;

            bool overlapped = false;
            hit = default;

            switch (lhs.Shape)
            {
                case ColliderShape.Obb:
                    switch (rhs.Shape)
                    {
                        case ColliderShape.Obb:    overlapped = ObbObb(lhs, rhs, out hit);    break;
                        case ColliderShape.Circle: overlapped = ObbCircle(lhs, rhs, out hit); break;
                    }
                    break;

                case ColliderShape.Circle:
                    switch (rhs.Shape)
                    {
                        case ColliderShape.Circle: overlapped = CircleCircle(lhs, rhs, out hit); break;
                    }
                    break;
            }

            // 순서를 뒤집었으면 떼어내는 방향도 뒤집어야 a 기준이 됨.
            if (flip)
            {
                hit.Normal = -hit.Normal;
            }

            return overlapped;
        }

        /// <summary>
        /// 분리축 정리(SAT). 두 사각형의 축 4개 중 하나라도 분리되면 겹치지 않음.
        /// 다 겹치면 그중 가장 얕은 축이 최소 이동 벡터(MTV)가 됨.
        /// </summary>
        private static bool ObbObb(in ColliderWorld a, in ColliderWorld b, out ColliderHit hit)
        {
            float2 delta = b.Center - a.Center;
            hit = new ColliderHit { Depth = float.MaxValue };

            return Overlap(delta, a.AxisX, a, b, ref hit)
                   && Overlap(delta, a.AxisY, a, b, ref hit)
                   && Overlap(delta, b.AxisX, a, b, ref hit)
                   && Overlap(delta, b.AxisY, a, b, ref hit);
        }

        /// <summary>
        /// 주어진 축에 두 사각형을 투영해 겹친 길이를 구함. 틈이 있으면 false.
        /// 지금까지 중 가장 얕으면 <paramref name="hit"/>을 갱신함.
        /// </summary>
        private static bool Overlap(
            float2 delta,
            float2 axis,
            in ColliderWorld a,
            in ColliderWorld b,
            ref ColliderHit hit)
        {
            float radiusA = a.Extents.x * math.abs(math.dot(axis, a.AxisX))
                            + a.Extents.y * math.abs(math.dot(axis, a.AxisY));

            float radiusB = b.Extents.x * math.abs(math.dot(axis, b.AxisX))
                            + b.Extents.y * math.abs(math.dot(axis, b.AxisY));

            float distance = math.dot(delta, axis);
            float depth = radiusA + radiusB - math.abs(distance);

            if (depth <= 0.0F)
            {
                return false;
            }

            if (depth < hit.Depth)
            {
                hit.Depth = depth;

                // b가 축의 양수 쪽에 있으면 a는 음수 쪽으로 물러나야 함.
                hit.Normal = distance > 0.0F ? -axis : axis;
            }

            return true;
        }

        /// <summary>
        /// 원의 중심을 사각형 로컬 좌표로 옮기면 축 정렬 문제로 바뀜.
        /// 사각형 안으로 클램프한 최근접점과의 거리로 판정함.
        /// </summary>
        private static bool ObbCircle(in ColliderWorld obb, in ColliderWorld circle, out ColliderHit hit)
        {
            float radius = circle.Extents.x;
            float2 delta = circle.Center - obb.Center;
            float2 local = new float2(math.dot(delta, obb.AxisX), math.dot(delta, obb.AxisY));
            float2 outward = local - math.clamp(local, -obb.Extents, obb.Extents);
            float outwardSq = math.lengthsq(outward);

            hit = default;

            // 원의 중심이 사각형 밖에 있는 경우.
            if (outwardSq > 0.0F)
            {
                if (outwardSq > radius * radius)
                {
                    return false;
                }

                float distance = math.sqrt(outwardSq);
                float2 direction = outward / distance;

                hit.Depth = radius - distance;
                hit.Normal = -(direction.x * obb.AxisX + direction.y * obb.AxisY);

                return true;
            }

            // 중심이 안쪽까지 들어왔으면 가장 가까운 변 쪽으로 밀어냄.
            float2 toEdge = obb.Extents - math.abs(local);

            if (toEdge.x < toEdge.y)
            {
                hit.Depth = toEdge.x + radius;
                hit.Normal = local.x > 0.0F ? -obb.AxisX : obb.AxisX;
            }
            else
            {
                hit.Depth = toEdge.y + radius;
                hit.Normal = local.y > 0.0F ? -obb.AxisY : obb.AxisY;
            }

            return true;
        }

        private static bool CircleCircle(in ColliderWorld a, in ColliderWorld b, out ColliderHit hit)
        {
            float radius = a.Extents.x + b.Extents.x;
            float2 delta = b.Center - a.Center;
            float distanceSq = math.lengthsq(delta);

            hit = default;

            if (distanceSq > radius * radius)
            {
                return false;
            }

            float distance = math.sqrt(distanceSq);

            hit.Depth = radius - distance;

            // 중심이 정확히 겹치면 방향을 정할 수 없어 아무 축이나 씀.
            hit.Normal = distance > 0.0F ? -delta / distance : new float2(1.0F, 0.0F);

            return true;
        }
    }
}