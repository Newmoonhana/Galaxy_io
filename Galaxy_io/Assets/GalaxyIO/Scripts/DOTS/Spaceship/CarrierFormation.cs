using Unity.Collections;
using Unity.Mathematics;

namespace GalaxyIO.DOTS.Spaceship
{
    /// <summary>
    /// 함재기 진형 헬퍼. 진형은 중심을 둘러싼 여러 겹의 고리이고, 자리는 고리 위의 점.
    /// </summary>
    public static class CarrierFormation
    {
        // 고리마다 첫 자리를 이만큼 돌려 안쪽 고리 자리와 겹쳐 보이지 않게 함.
        private const float RingTwist = 2.39996F;

        /// <summary>
        /// ring 번째 고리의 반지름.
        /// </summary>
        public static float RingRadius(float first, float gap, int ring)
        {
            return first + gap * ring;
        }

        /// <summary>
        /// 반지름 radius 인 고리에 slotGap 간격으로 들어가는 자리 수.
        /// </summary>
        public static int RingCapacity(float radius, float slotGap)
        {
            return math.max(1, (int)math.floor(2.0F * math.PI * radius / math.max(slotGap, 0.01F)));
        }

        /// <summary>
        /// 각도(라디안)의 방향. 0 이 위쪽, 반시계로 커짐.
        /// </summary>
        public static float2 Direction(float angle)
        {
            math.sincos(angle, out float sin, out float cos);
            return new float2(-sin, cos);
        }

        /// <summary>
        /// 안쪽 고리부터 채운 count 개 자리 중 index 번째의 중심 기준 위치.
        /// 마지막 고리는 남은 자리 수로 고르게 나눔.
        /// </summary>
        public static float2 RingSlot(int index, int count, float first, float gap, float slotGap)
        {
            int start = 0;

            for (int ring = 0; ; ring++)
            {
                float radius = RingRadius(first, gap, ring);
                int used = math.min(RingCapacity(radius, slotGap), count - start);

                if (index < start + used)
                {
                    return Direction(ring * RingTwist + 2.0F * math.PI * (index - start) / used) * radius;
                }

                start += used;
            }
        }

        /// <summary>
        /// 이동 거리 합이 가장 짧은 함재기 - 자리 짝을 찾음. (헝가리안 알고리즘, O(rows² x cols))
        /// cost 는 rows x cols 행렬(함재기 x 자리)이고 rows &lt;= cols 여야 함. assignment[함재기] = 자리.
        /// 거리 합이 가장 짧은 짝은 직선 경로끼리 서로 엇갈리지 않음.
        /// </summary>
        public static void Assign(NativeArray<float> cost, int rows, int cols, NativeArray<int> assignment)
        {
            // 1부터 세는 잠재값(u, v)과 자리마다 짝지은 함재기(p, 0 이면 없음).
            var u = new NativeArray<float>(rows + 1, Allocator.Temp);
            var v = new NativeArray<float>(cols + 1, Allocator.Temp);
            var p = new NativeArray<int>(cols + 1, Allocator.Temp);
            var way = new NativeArray<int>(cols + 1, Allocator.Temp);
            var minv = new NativeArray<float>(cols + 1, Allocator.Temp);
            var used = new NativeArray<bool>(cols + 1, Allocator.Temp);

            for (int i = 1; i <= rows; i++)
            {
                // 함재기 i 를 넣고 증가 경로를 찾을 때까지 잠재값을 고침.
                p[0] = i;
                int j0 = 0;

                for (int j = 0; j <= cols; j++)
                {
                    minv[j] = float.MaxValue;
                    used[j] = false;
                }

                do
                {
                    used[j0] = true;
                    int i0 = p[j0];
                    float delta = float.MaxValue;
                    int j1 = 0;

                    for (int j = 1; j <= cols; j++)
                    {
                        if (used[j])
                        {
                            continue;
                        }

                        float reduced = cost[(i0 - 1) * cols + (j - 1)] - u[i0] - v[j];

                        if (reduced < minv[j])
                        {
                            minv[j] = reduced;
                            way[j] = j0;
                        }

                        if (minv[j] < delta)
                        {
                            delta = minv[j];
                            j1 = j;
                        }
                    }

                    for (int j = 0; j <= cols; j++)
                    {
                        if (used[j])
                        {
                            u[p[j]] += delta;
                            v[j] -= delta;
                        }
                        else
                        {
                            minv[j] -= delta;
                        }
                    }

                    j0 = j1;
                }
                while (p[j0] != 0);

                // 찾은 경로를 따라 짝을 뒤집음.
                do
                {
                    int j1 = way[j0];
                    p[j0] = p[j1];
                    j0 = j1;
                }
                while (j0 != 0);
            }

            for (int j = 1; j <= cols; j++)
            {
                if (p[j] != 0)
                {
                    assignment[p[j] - 1] = j - 1;
                }
            }

            u.Dispose();
            v.Dispose();
            p.Dispose();
            way.Dispose();
            minv.Dispose();
            used.Dispose();
        }
    }
}
