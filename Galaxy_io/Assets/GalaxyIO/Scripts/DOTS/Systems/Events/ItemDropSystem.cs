using GalaxyIO.DOTS.Components.Actor;
using GalaxyIO.DOTS.Components.Message;
using GalaxyIO.DOTS.Components.Blob;
using GalaxyIO.DOTS.Components.Singleton;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.NetCode;
using Unity.Transforms;
using Random = Unity.Mathematics.Random;

namespace GalaxyIO.DOTS.Systems
{
    /// <summary>
    /// 체력이 0이 된 적 우주선에서 드랍 테이블을 굴려 아이템 스폰 메시지를 남기는 시스템.
    /// 플레이어가 막타나 어시스트를 했을 때만 떨어뜨림. 플레이어 사망 드랍은 InventoryService 가 처리함.
    /// </summary>
    [BurstCompile]
    [WorldSystemFilter(WorldSystemFilterFlags.ServerSimulation)]
    [UpdateInGroup(typeof(EventSystemGroup))]
    [UpdateAfter(typeof(DamageNotifySystem))]
    [UpdateBefore(typeof(SpaceshipDeathSystem))]
    public partial struct ItemDropSystem : ISystem
    {
        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<NetworkTime>();
            state.RequireForUpdate<PrefabRegistryComponent>();
            state.RequireForUpdate(
                SystemAPI.QueryBuilder()
                    .WithAll<SpaceshipSpecComponent, LocalTransform, DropTableComponent, KillingBlowComponent, DamageRecordElement>()
                    .Build()
            );
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            Entity itemPrefab = SystemAPI.GetSingleton<PrefabRegistryComponent>().ItemEntity;

            // 레지스트리에 아이템 Entity 가 없으면 베이킹 설정 문제. 스폰할 것이 없음.
            if (itemPrefab == Entity.Null)
            {
                return;
            }

            // 미끄러지는 속력과 줍기 대기 시간은 프리팹 값을 씀.
            ItemComponent item = SystemAPI.GetComponent<ItemComponent>(itemPrefab);
            DynamicBuffer<ItemSpawnMessage> spawns = SystemAPI.GetSingletonBuffer<ItemSpawnMessage>();

            double time = SystemAPI.Time.ElapsedTime;
            uint tick = SystemAPI.GetSingleton<NetworkTime>().ServerTick.SerializedData;
            var random = Random.CreateFromIndex(tick & 0x7FFFFFFFu);

            foreach (
                var (spec, transform, dropTable, blow, records) in
                SystemAPI.Query<
                    RefRO<SpaceshipSpecComponent>,
                    RefRO<LocalTransform>,
                    RefRO<DropTableComponent>,
                    RefRO<KillingBlowComponent>,
                    DynamicBuffer<DamageRecordElement>>()
            ) {
                if (spec.ValueRO.Health > 0.0F)
                {
                    continue;
                }

                // 막타가 플레이어가 아니면 유효한 어시스트 기록이 있어야 함. 기록은 플레이어 데미지만 남음.
                bool byPlayer = blow.ValueRO.NetworkId > 0;

                for (int i = 0; i < records.Length && !byPlayer; i++)
                {
                    byPlayer = DamageRecord.IsAlive(records[i], time);
                }

                if (!byPlayer)
                {
                    continue;
                }

                ref BlobArray<DropRollBlob> rolls = ref dropTable.ValueRO.Value.Value.Rolls;

                for (int r = 0; r < rolls.Length; r++)
                {
                    ref DropRollBlob roll = ref rolls[r];

                    if (roll.TotalWeight <= 0 || random.NextFloat() >= roll.Chance)
                    {
                        continue;
                    }

                    // 가중치 구간에 떨어진 항목 하나를 고름.
                    int pick = random.NextInt(roll.TotalWeight);
                    int index = 0;

                    while (pick >= roll.Entries[index].Weight)
                    {
                        pick -= roll.Entries[index].Weight;
                        index++;
                    }

                    ref DropEntryBlob entry = ref roll.Entries[index];

                    spawns.Add(new ItemSpawnMessage
                    {
                        Prefab      = itemPrefab,
                        Id          = entry.ItemId,
                        Count       = random.NextInt(entry.MinCount, entry.MaxCount + 1),
                        Position    = transform.ValueRO.Position,
                        Velocity    = random.NextFloat2Direction() * random.NextFloat(0.5F, 1.0F) * item.SlideSpeed,
                        PickupDelay = item.DropPickupDelay,
                    });
                }
            }
        }
    }
}
