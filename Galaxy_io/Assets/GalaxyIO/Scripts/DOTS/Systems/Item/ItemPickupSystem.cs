using GalaxyIO.DOTS.Components.Actor;
using GalaxyIO.DOTS.Components.Message;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
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
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            float deltaTime = SystemAPI.Time.DeltaTime;

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

                // 동시에 닿았으면 버퍼 맨 앞 플레이어가 주움.
                Entity picker = Entity.Null;

                for (int i = 0; i < overlaps.Length; i++)
                {
                    if (overlaps[i].Contact && SystemAPI.HasBuffer<InventoryItemElement>(overlaps[i].Message.Other))
                    {
                        picker = overlaps[i].Message.Other;
                        break;
                    }
                }

                if (picker != Entity.Null)
                {
                    DynamicBuffer<InventoryItemElement> inventory = SystemAPI.GetBuffer<InventoryItemElement>(picker);
                    int slot = -1;

                    for (int i = 0; i < inventory.Length; i++)
                    {
                        if (inventory[i].Id == item.ValueRO.Id)
                        {
                            slot = i;
                            break;
                        }
                    }

                    if (slot < 0)
                    {
                        inventory.Add(new InventoryItemElement { Id = item.ValueRO.Id, Count = item.ValueRO.Count });
                    }
                    else
                    {
                        InventoryItemElement stack = inventory[slot];
                        stack.Count += item.ValueRO.Count;
                        inventory[slot] = stack;
                    }

                    // 줍기 효과음은 주운 본인만 들음.
                    if (SystemAPI.HasComponent<GhostOwner>(picker)
                        && connections.TryGetValue(SystemAPI.GetComponent<GhostOwner>(picker).NetworkId, out Entity target))
                    {
                        Entity rpcEntity = ecb.CreateEntity();

                        ecb.AddComponent(rpcEntity, new ItemPickedUpRPC { Position = transform.ValueRO.Position.xy });
                        ecb.AddComponent(rpcEntity, new SendRpcCommandRequest { TargetConnection = target });
                    }

                    ecb.DestroyEntity(entity);
                }
                else if (item.ValueRO.Lifetime <= 0.0F)
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
