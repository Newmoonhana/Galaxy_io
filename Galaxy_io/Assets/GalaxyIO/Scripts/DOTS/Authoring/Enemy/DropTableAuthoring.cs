using GalaxyIO.DOTS.Components.Actor;
using GalaxyIO.DOTS.Components.Blob;
using GalaxyIO.DOTS.Components.Singleton;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

namespace GalaxyIO.DOTS.Authoring
{
    /// <summary>
    /// 파괴 시 떨어뜨릴 아이템 정의.
    /// </summary>
    public sealed class DropTableAuthoring : MonoBehaviour
    {
        [SerializeField]
        [Tooltip("베이킹할 드랍 테이블")]
        public DropTableAsset Table;
    }

    public sealed class DropTableBaker : Baker<DropTableAuthoring>
    {
        public override void Bake(DropTableAuthoring authoring)
        {
            // 에셋 내용이 바뀌면 다시 베이킹.
            DependsOn(authoring.Table);

            if (authoring.Table == null)
            {
                Debug.LogWarning("DropTable: 드랍 테이블 에셋이 비어 있음.", authoring);
                return;
            }

            Entity entity = GetEntity(TransformUsageFlags.Dynamic);

            var builder = new BlobBuilder(Allocator.Temp);
            ref DropTableBlob table = ref builder.ConstructRoot<DropTableBlob>();

            BlobBuilderArray<DropRollBlob> rolls = builder.Allocate(ref table.Rolls, authoring.Table.Rolls.Count);
            var entries = new NativeList<DropEntryBlob>(Allocator.Temp);

            for (int r = 0; r < rolls.Length; r++)
            {
                DropTableAsset.Roll roll = authoring.Table.Rolls[r];
                entries.Clear();

                int totalWeight = 0;

                foreach (DropTableAsset.Entry entry in roll.Entries)
                {
                    DependsOn(entry.Item);

                    if (entry.Item == null || entry.Item.Id == ItemId.None || entry.Weight <= 0)
                    {
                        Debug.LogWarning("DropTable: 아이템이 비어 있거나 가중치가 0인 항목을 건너뜀.", authoring.Table);
                        continue;
                    }

                    entries.Add(new DropEntryBlob
                    {
                        ItemId   = entry.Item.Id,
                        MinCount = math.max(math.min(entry.CountRange.x, entry.CountRange.y), 1),
                        MaxCount = math.max(math.max(entry.CountRange.x, entry.CountRange.y), 1),
                        Weight   = entry.Weight,
                    });

                    totalWeight += entry.Weight;
                }

                rolls[r].Chance = roll.Chance;
                rolls[r].TotalWeight = totalWeight;

                BlobBuilderArray<DropEntryBlob> array = builder.Allocate(ref rolls[r].Entries, entries.Length);

                for (int i = 0; i < entries.Length; i++)
                {
                    array[i] = entries[i];
                }
            }

            BlobAssetReference<DropTableBlob> blob = builder.CreateBlobAssetReference<DropTableBlob>(Allocator.Persistent);
            builder.Dispose();
            entries.Dispose();

            // 같은 내용의 블롭은 하나로 합쳐져 인스턴스끼리 공유됨.
            AddBlobAsset(ref blob, out _);

            AddComponent(entity, new DropTableComponent { Value = blob });
        }
    }
}
