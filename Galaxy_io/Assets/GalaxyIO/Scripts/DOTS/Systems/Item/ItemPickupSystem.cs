using GalaxyIO.DOTS.Components.Actor;
using GalaxyIO.DOTS.Components.Message;
using GalaxyIO.DOTS.Components.Singleton;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.NetCode;
using Unity.Transforms;

namespace GalaxyIO.DOTS.Systems
{
    /// <summary>
    /// 플레이어에 닿은 아이템을 인벤토리에 넣고 주운 플레이어에게 알리는 시스템.
    /// 수명이 다한 아이템도 제거함.
    /// </summary>
    [BurstCompile]
    [WorldSystemFilter(WorldSystemFilterFlags.ServerSimulation)]
    [UpdateInGroup(typeof(DespawnSystemGroup))]
    public partial struct ItemPickupSystem : ISystem
    {
        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<ItemComponent>();
            state.RequireForUpdate<ItemRegistryElement>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            float deltaTime = SystemAPI.Time.DeltaTime;
            DynamicBuffer<ItemRegistryElement> registry = SystemAPI.GetSingletonBuffer<ItemRegistryElement>(true);

            // NetworkId -> 접속
            var connections = new NativeHashMap<int, Entity>(4, Allocator.Temp);

            foreach (
                var (networkId, connection) in
                SystemAPI.Query<RefRO<NetworkId>>()
                    .WithAll<NetworkStreamInGame>()
                    .WithEntityAccess()
            ) {
                connections[networkId.ValueRO.Value] = connection;
            }

            var ecb = new EntityCommandBuffer(Allocator.Temp);

            foreach (
                var (item, overlaps, transform, entity) in
                SystemAPI.Query<RefRW<ItemComponent>, DynamicBuffer<ItemOverlapElement>, RefRO<LocalTransform>>()
                    .WithEntityAccess()
            ) {
                item.ValueRW.Lifetime -= deltaTime;
                item.ValueRW.PickupDelay = math.max(item.ValueRO.PickupDelay - deltaTime, 0.0F);

                // 동시에 닿았으면 버퍼 앞쪽 플레이어부터 주움. 인벤토리가 차면 다음 플레이어에게 넘어감.
                for (int i = 0; i < overlaps.Length && item.ValueRO.Count > 0 && item.ValueRO.PickupDelay <= 0.0F; i++)
                {
                    Entity picker = overlaps[i].Message.Other;

                    if (!overlaps[i].Contact || !SystemAPI.HasBuffer<InventorySlotElement>(picker))
                    {
                        continue;
                    }

                    int inserted = Inventory.Insert(
                        SystemAPI.GetBuffer<InventorySlotElement>(picker),
                        registry,
                        item.ValueRO.Id,
                        item.ValueRO.Count
                    );

                    if (inserted == 0)
                    {
                        continue;
                    }

                    item.ValueRW.Count -= inserted;

                    // 줍기 효과음은 주운 본인만 들음.
                    if (SystemAPI.HasComponent<GhostOwner>(picker)
                        && connections.TryGetValue(SystemAPI.GetComponent<GhostOwner>(picker).NetworkId, out Entity target))
                    {
                        Entity rpcEntity = ecb.CreateEntity();

                        ecb.AddComponent(rpcEntity, new ItemPickedUpRPC { Position = transform.ValueRO.Position.xy });
                        ecb.AddComponent(rpcEntity, new SendRpcCommandRequest { TargetConnection = target });
                    }
                }

                // 다 들어가지 못한 나머지는 월드에 남음.
                if (item.ValueRO.Count <= 0 || item.ValueRO.Lifetime <= 0.0F)
                {
                    ecb.DestroyEntity(entity);
                }
            }

            ecb.Playback(state.EntityManager);
            ecb.Dispose();
            connections.Dispose();
        }
    }
}
