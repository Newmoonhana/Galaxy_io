using GalaxyIO.DOTS.Components.Actor;
using GalaxyIO.DOTS.Components.Message;
using GalaxyIO.DOTS.Components.Singleton;
using GalaxyIO.DOTS.Spaceship;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.NetCode;
using Unity.Transforms;

namespace GalaxyIO.DOTS.Systems
{
    /// <summary>
    /// <see cref="InventoryDropRPC"/>를 받아 요청한 플레이어 인벤토리 한 칸에서 아이템을 꺼내
    /// 기수 방향으로 던지는 시스템.
    /// </summary>
    [BurstCompile]
    [WorldSystemFilter(WorldSystemFilterFlags.ServerSimulation)]
    public partial struct InventoryDropSystem : ISystem
    {
        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<PrefabRegistryElement>();
            state.RequireForUpdate<ItemRegistryElement>();
            state.RequireForUpdate<InventoryDropRPC>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            Entity itemPrefab = SystemAPI
                .GetSingletonBuffer<PrefabRegistryElement>(true)
                .Find(PrefabId.ItemEntity);

            DynamicBuffer<ItemRegistryElement> registry = SystemAPI.GetSingletonBuffer<ItemRegistryElement>(true);

            // NetworkId -> 우주선
            var ships = new NativeHashMap<int, Entity>(4, Allocator.Temp);

            foreach (
                var (owner, ship) in
                SystemAPI.Query<RefRO<GhostOwner>>()
                    .WithAll<InventorySlotElement, LocalTransform>()
                    .WithEntityAccess()
            ) {
                ships[owner.ValueRO.NetworkId] = ship;
            }

            var ecb = new EntityCommandBuffer(Allocator.Temp);

            foreach (
                var (request, rpc, rpcEntity) in
                SystemAPI.Query<RefRO<ReceiveRpcCommandRequest>, RefRO<InventoryDropRPC>>()
                    .WithEntityAccess()
            ) {
                ecb.DestroyEntity(rpcEntity);

                int networkId = SystemAPI.GetComponent<NetworkId>(request.ValueRO.SourceConnection).Value;
                int index = rpc.ValueRO.Slot;

                // 파괴된 뒤 도착한 요청, 레지스트리에 아이템 Entity 가 없는 경우는 버림.
                if (itemPrefab == Entity.Null || !ships.TryGetValue(networkId, out Entity ship))
                {
                    continue;
                }

                DynamicBuffer<InventorySlotElement> slots = SystemAPI.GetBuffer<InventorySlotElement>(ship);

                if (index >= slots.Length || !registry.Contains(slots[index].Id))
                {
                    continue;
                }

                InventorySlotElement slot = slots[index];
                int count = math.min((int)rpc.ValueRO.Count, slot.Count);
                LocalTransform shipTransform = SystemAPI.GetComponent<LocalTransform>(ship);

                // 프리팹의 설정값과 회전/크기는 그대로 쓰고 아이템 종류, 속도, 튕김, 위치만 바꿈.
                ItemComponent item = SystemAPI.GetComponent<ItemComponent>(itemPrefab);
                item.Id = slot.Id;
                item.Count = count;
                item.Launch(SpaceshipHeading.FromRotation(shipTransform.Rotation) * item.ThrowSpeed, item.ThrowPickupDelay);

                ColliderComponent collider = SystemAPI.GetComponent<ColliderComponent>(itemPrefab);
                collider.Extents = registry[(int)slot.Id].HalfExtents;

                LocalTransform transform = SystemAPI.GetComponent<LocalTransform>(itemPrefab);
                transform.Position = shipTransform.Position;

                Entity entity = ecb.Instantiate(itemPrefab);
                ecb.SetComponent(entity, item);
                ecb.SetComponent(entity, collider);
                ecb.SetComponent(entity, transform);

                slot.Count -= (byte)count;

                if (slot.Count == 0)
                {
                    slot.Id = ItemId.None;
                }

                slots[index] = slot;
            }

            ecb.Playback(state.EntityManager);
            ecb.Dispose();
            ships.Dispose();
        }
    }
}
