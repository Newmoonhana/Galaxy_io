using System;
using System.Collections.Generic;
using GalaxyIO.DOTS.Components.Blob;
using GalaxyIO.DOTS.Components.Singleton;
using GalaxyIO.DOTS.Networking;
using Newmoonhana.Runtime.Utils.DEBUG;
using Unity.Collections;
using Unity.Entities;

namespace GalaxyIO.DOTS.Systems
{
    /// <summary>
    /// 데디케이티드 서버 설정의 드랍 테이블로 베이킹된 드랍 테이블을 덮어쓰는 시스템.
    /// 설정에 없는 적 종류는 베이킹된 테이블을 설정 파일에 채워 둠.
    /// </summary>
    [WorldSystemFilter(WorldSystemFilterFlags.ServerSimulation)]
    [UpdateInGroup(typeof(InitializationSystemGroup))]
    public partial struct DropTableConfigSystem : ISystem
    {
        // 설정으로 만든 블롭. 베이킹된 블롭과 달리 직접 버려야 함.
        private NativeList<BlobAssetReference<DropTableBlob>> created;

        public void OnCreate(ref SystemState state)
        {
            created = new NativeList<BlobAssetReference<DropTableBlob>>(Allocator.Persistent);
            state.RequireForUpdate<DropTableRegistryElement>();
        }

        public void OnDestroy(ref SystemState state)
        {
            foreach (BlobAssetReference<DropTableBlob> blob in created)
            {
                blob.Dispose();
            }

            created.Dispose();
        }

        public void OnUpdate(ref SystemState state)
        {
            // 레지스트리가 들어온 첫 프레임에 한 번만 적용함.
            state.Enabled = false;

            DedicatedServerConfig config = DedicatedServerConfig.Current;

            if (config == null)
            {
                return;
            }

            DynamicBuffer<DropTableRegistryElement> registry = SystemAPI.GetSingletonBuffer<DropTableRegistryElement>();
            var configured = new HashSet<EnemyType>();

            foreach (DedicatedServerConfig.DropTable table in config.DropTables)
            {
                if (!Enum.TryParse(table.Enemy, true, out EnemyType enemy) || enemy == EnemyType.None || (int)enemy >= registry.Length)
                {
                    DebugLog.LogError($"서버 설정: 알 수 없는 적 종류 {table.Enemy}. 드랍 테이블을 건너뜀.");
                    continue;
                }

                if (!configured.Add(enemy))
                {
                    DebugLog.LogError($"서버 설정: 드랍 테이블의 적 종류 중복. 뒤의 테이블을 버림. ({enemy})");
                    continue;
                }

                var rolls = new List<DropRoll>();

                foreach (DedicatedServerConfig.DropRoll roll in table.Rolls)
                {
                    var entries = new List<DropEntryBlob>();

                    foreach (DedicatedServerConfig.DropEntry entry in roll.Entries)
                    {
                        if (!Enum.TryParse(entry.Item, true, out ItemId item) || !Enum.IsDefined(typeof(ItemId), item)
                            || item == ItemId.None || entry.Weight <= 0)
                        {
                            DebugLog.LogError($"서버 설정: {enemy} 드랍 테이블의 아이템({entry.Item})이 잘못됐거나 가중치가 0임. 항목을 건너뜀.");
                            continue;
                        }

                        entries.Add(new DropEntryBlob
                        {
                            ItemId   = item,
                            MinCount = entry.Min,
                            MaxCount = entry.Max,
                            Weight   = entry.Weight,
                        });
                    }

                    rolls.Add(new DropRoll { Chance = roll.Chance, Entries = entries });
                }

                BlobAssetReference<DropTableBlob> blob = DropTables.Create(rolls);
                created.Add(blob);

                registry[(int)enemy] = new DropTableRegistryElement { Table = blob };
            }

            // 설정에 없는 적 종류는 베이킹된 테이블을 그대로 옮겨 적음. 서버 운영자가 고칠 수 있게 함.
            bool filled = false;

            for (int i = 1; i < registry.Length; i++)
            {
                BlobAssetReference<DropTableBlob> baked = registry[i].Table;

                if (configured.Contains((EnemyType)i) || !baked.IsCreated)
                {
                    continue;
                }

                var table = new DedicatedServerConfig.DropTable { Enemy = ((EnemyType)i).ToString() };
                ref BlobArray<DropRollBlob> rolls = ref baked.Value.Rolls;

                for (int r = 0; r < rolls.Length; r++)
                {
                    var roll = new DedicatedServerConfig.DropRoll { Chance = rolls[r].Chance };

                    for (int e = 0; e < rolls[r].Entries.Length; e++)
                    {
                        ref DropEntryBlob entry = ref rolls[r].Entries[e];

                        roll.Entries.Add(new DedicatedServerConfig.DropEntry
                        {
                            Item   = entry.ItemId.ToString(),
                            Min    = entry.MinCount,
                            Max    = entry.MaxCount,
                            Weight = entry.Weight,
                        });
                    }

                    table.Rolls.Add(roll);
                }

                config.DropTables.Add(table);
                filled = true;
            }

            if (filled)
            {
                config.Save();
            }
        }
    }
}
