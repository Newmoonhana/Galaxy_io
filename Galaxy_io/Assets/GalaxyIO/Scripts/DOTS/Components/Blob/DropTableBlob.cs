using System.Collections.Generic;
using GalaxyIO.DOTS.Components.Singleton;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

namespace GalaxyIO.DOTS.Components.Blob
{
    /// <summary>
    /// 파괴 시 떨어뜨릴 아이템 목록. Roll 마다 따로 굴림.
    /// </summary>
    public struct DropTableBlob
    {
        public BlobArray<DropRollBlob> Rolls;
    }

    /// <summary>
    /// 확률에 성공하면 항목 중 하나를 가중치로 골라 떨어뜨림.
    /// </summary>
    public struct DropRollBlob
    {
        // Roll 성공 확률 (0 ~ 1)
        public float Chance;

        // 항목 가중치 합
        public int TotalWeight;

        public BlobArray<DropEntryBlob> Entries;
    }

    public struct DropEntryBlob
    {
        public ItemId ItemId;

        // 한 번에 떨어뜨릴 개수 범위 (양 끝 포함)
        public int MinCount;
        public int MaxCount;

        // 같은 Roll 안에서 뽑힐 비중
        public int Weight;
    }

    /// <summary>
    /// 적 종류별 드랍 테이블. 버퍼 인덱스가 곧 <see cref="EnemyType"/> 값. 월드당 한 개.
    /// </summary>
    [InternalBufferCapacity(0)]
    public struct DropTableRegistryElement : IBufferElementData
    {
        // 빈 칸이면 IsCreated 가 false.
        public BlobAssetReference<DropTableBlob> Table;
    }

    /// <summary>
    /// <see cref="DropTables.Create"/>에 넘기는 Roll 하나.
    /// </summary>
    public struct DropRoll
    {
        public float Chance;
        public List<DropEntryBlob> Entries;
    }

    public static class DropTables
    {
        /// <summary>
        /// 드랍 테이블 블롭 생성. 개수 범위는 (최소, 최대) 순서로 맞추고 1 이상으로 올림.
        /// </summary>
        public static BlobAssetReference<DropTableBlob> Create(List<DropRoll> rolls)
        {
            var builder = new BlobBuilder(Allocator.Temp);
            ref DropTableBlob table = ref builder.ConstructRoot<DropTableBlob>();

            BlobBuilderArray<DropRollBlob> rollArray = builder.Allocate(ref table.Rolls, rolls.Count);

            for (int r = 0; r < rolls.Count; r++)
            {
                List<DropEntryBlob> entries = rolls[r].Entries;
                BlobBuilderArray<DropEntryBlob> entryArray = builder.Allocate(ref rollArray[r].Entries, entries.Count);

                int totalWeight = 0;

                for (int i = 0; i < entries.Count; i++)
                {
                    DropEntryBlob entry = entries[i];

                    entryArray[i] = new DropEntryBlob
                    {
                        ItemId   = entry.ItemId,
                        MinCount = math.max(math.min(entry.MinCount, entry.MaxCount), 1),
                        MaxCount = math.max(math.max(entry.MinCount, entry.MaxCount), 1),
                        Weight   = entry.Weight,
                    };

                    totalWeight += entry.Weight;
                }

                rollArray[r].Chance = rolls[r].Chance;
                rollArray[r].TotalWeight = totalWeight;
            }

            BlobAssetReference<DropTableBlob> blob = builder.CreateBlobAssetReference<DropTableBlob>(Allocator.Persistent);
            builder.Dispose();

            return blob;
        }

        /// <summary>
        /// 적 종류의 드랍 테이블. 없으면 false.
        /// </summary>
        public static bool TryGet(this DynamicBuffer<DropTableRegistryElement> registry, EnemyType enemy, out BlobAssetReference<DropTableBlob> table)
        {
            table = (int)enemy < registry.Length ? registry[(int)enemy].Table : default;
            return table.IsCreated;
        }
    }
}
