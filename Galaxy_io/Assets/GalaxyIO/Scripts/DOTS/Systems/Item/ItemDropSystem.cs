using GalaxyIO.DOTS.Components.Actor;
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
    /// 체력이 0이 된 우주선의 드랍 테이블을 굴려 아이템 Entity 를 스폰하는 시스템.
    /// 플레이어가 막타나 어시스트를 했을 때만 떨어뜨림.
    /// </summary>
    [BurstCompile]
    [WorldSystemFilter(WorldSystemFilterFlags.ServerSimulation)]
    [UpdateInGroup(typeof(DespawnSystemGroup))]
    [UpdateBefore(typeof(SpaceshipDeathSystem))]
    public partial struct ItemDropSystem : ISystem
    {
        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<NetworkTime>();
            state.RequireForUpdate<PrefabRegistryElement>();
            state.RequireForUpdate<ItemRegistryElement>();
            state.RequireForUpdate(
                SystemAPI.QueryBuilder()
                    .WithAll<SpaceshipSpecComponent, LocalTransform, DropTableComponent, KillingBlowComponent, DamageRecordElement>()
                    .Build()
            );
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            Entity itemPrefab = SystemAPI
                .GetSingletonBuffer<PrefabRegistryElement>(true)
                .Find(PrefabId.ItemEntity);

            // 레지스트리에 아이템 Entity 가 없으면 베이킹 설정 문제. 스폰할 것이 없음.
            if (itemPrefab == Entity.Null)
            {
                return;
            }

            DynamicBuffer<ItemRegistryElement> registry = SystemAPI.GetSingletonBuffer<ItemRegistryElement>(true);

            // 프리팹의 설정값과 회전/크기는 그대로 쓰고 아이템 종류, 속도, 튕김, 위치만 바꿈.
            ItemComponent itemTemplate = SystemAPI.GetComponent<ItemComponent>(itemPrefab);
            ColliderComponent colliderTemplate = SystemAPI.GetComponent<ColliderComponent>(itemPrefab);
            LocalTransform transformTemplate = SystemAPI.GetComponent<LocalTransform>(itemPrefab);

            double time = SystemAPI.Time.ElapsedTime;
            uint tick = SystemAPI.GetSingleton<NetworkTime>().ServerTick.SerializedData;
            var random = Random.CreateFromIndex(tick & 0x7FFFFFFFu);

            var ecb = new EntityCommandBuffer(Allocator.Temp);

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

                    if (!registry.Contains(entry.ItemId))
                    {
                        continue;
                    }

                    Entity item = ecb.Instantiate(itemPrefab);

                    itemTemplate.Id       = entry.ItemId;
                    itemTemplate.Count    = random.NextInt(entry.MinCount, entry.MaxCount + 1);
                    itemTemplate.Velocity = random.NextFloat2Direction()
                                            * random.NextFloat(0.5F, 1.0F) * itemTemplate.SlideSpeed;

                    // 미끄러지는 쪽으로 구르듯 기울어짐. 오른쪽이면 시계 방향.
                    itemTemplate.BounceRemaining = itemTemplate.BounceDuration;
                    itemTemplate.BounceSide = math.dot(itemTemplate.Velocity, new float2(1.0F, 0.0F)) >= 0.0F
                        ? -1.0F
                        : 1.0F;
                    ecb.SetComponent(item, itemTemplate);

                    colliderTemplate.Extents = registry[(int)entry.ItemId].HalfExtents;
                    ecb.SetComponent(item, colliderTemplate);

                    transformTemplate.Position = transform.ValueRO.Position;
                    ecb.SetComponent(item, transformTemplate);
                }
            }

            ecb.Playback(state.EntityManager);
            ecb.Dispose();
        }
    }
}
