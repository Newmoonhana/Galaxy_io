using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;

namespace GalaxyIO.DOTS.Collision
{
    /// <summary>
    /// 월드 XY 좌표를 정사각형 칸으로 나누는 공간 해시.
    /// </summary>
    public static class SpatialHash
    {
        public static int2 ToCell(float2 position, float cellSize)
        {
            return (int2)math.floor(position / cellSize);
        }
    }

    /// <summary>
    /// 경계 상자가 걸친 모든 칸에 배열 인덱스를 등록함.
    /// </summary>
    [BurstCompile]
    public struct BuildSpatialHashJob : IJob
    {
        [ReadOnly]
        public NativeList<ColliderProxy> Proxies;

        public NativeParallelMultiHashMap<int2, int> Cells;

        public float CellSize;

        public void Execute()
        {
            for (int i = 0; i < Proxies.Length; i++)
            {
                int2 min = SpatialHash.ToCell(Proxies[i].Bounds.Min, CellSize);
                int2 max = SpatialHash.ToCell(Proxies[i].Bounds.Max, CellSize);

                for (int y = min.y; y <= max.y; y++)
                {
                    for (int x = min.x; x <= max.x; x++)
                    {
                        Cells.Add(new int2(x, y), i);
                    }
                }
            }
        }
    }

    /// <summary>
    /// 질의 상자와 겹치는 충돌체 인덱스를 하나씩 돌려주는 반복자.
    /// 경계 상자가 겹치는 것만 돌려줌.
    /// </summary>
    public struct SpatialHashCandidates
    {
        private NativeParallelMultiHashMap<int2, int> cells;
        private NativeParallelMultiHashMapIterator<int2> iterator;
        private NativeList<ColliderProxy> proxies;
        private Aabb bounds;

        // 질의 상자가 걸친 첫 칸과 칸 수
        private int2 min;
        private int2 size;

        private float cellSize;

        // 다음에 볼 칸 번호
        private int cursor;

        // 지금 보고 있는 칸과 그 칸의 반복 중 여부
        private int2 cell;
        private bool inCell;

        public SpatialHashCandidates(
            NativeParallelMultiHashMap<int2, int> cells,
            NativeList<ColliderProxy> proxies,
            float cellSize,
            Aabb bounds)
        {
            this.cells = cells;
            this.iterator = default;
            this.proxies = proxies;
            this.bounds = bounds;
            this.cellSize = cellSize;
            this.min = SpatialHash.ToCell(bounds.Min, cellSize);
            this.size = SpatialHash.ToCell(bounds.Max, cellSize) - this.min + 1;
            this.cursor = 0;
            this.cell = this.min;
            this.inCell = false;
        }

        public bool Next(out int index)
        {
            while (true)
            {
                if (inCell)
                {
                    while (cells.TryGetNextValue(out index, ref iterator))
                    {
                        if (Accepts(index))
                        {
                            return true;
                        }
                    }

                    inCell = false;
                }

                if (cursor >= size.x * size.y)
                {
                    index = -1;

                    return false;
                }

                cell = min + new int2(cursor % size.x, cursor / size.x);
                cursor++;

                if (cells.TryGetFirstValue(cell, out index, out iterator))
                {
                    inCell = true;

                    if (Accepts(index))
                    {
                        return true;
                    }
                }
            }
        }

        // 같은 충돌체를 여러 칸에서 만나므로, 두 상자가 함께 걸친 첫 칸에서만 받음.
        private bool Accepts(int index)
        {
            Aabb target = proxies[index].Bounds;

            if (!bounds.Overlaps(target))
            {
                return false;
            }

            return math.all(cell == math.max(min, SpatialHash.ToCell(target.Min, cellSize)));
        }
    }
}
