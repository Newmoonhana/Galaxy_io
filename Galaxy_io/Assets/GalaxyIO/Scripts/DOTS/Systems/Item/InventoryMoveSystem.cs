using GalaxyIO.DOTS.Components.Actor;
using GalaxyIO.DOTS.Components.Message;
using GalaxyIO.DOTS.Components.Singleton;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.NetCode;

namespace GalaxyIO.DOTS.Systems
{
    /// <summary>
    /// <see cref="InventoryMoveRPC"/>를 받아 요청한 플레이어의 인벤토리 칸을 옮기는 시스템.
    /// 포탑 칸이 바뀌면 장착 포탑도 바꿈.
    /// </summary>
    [BurstCompile]
    [WorldSystemFilter(WorldSystemFilterFlags.ServerSimulation)]
    public partial struct InventoryMoveSystem : ISystem
    {
        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<ItemRegistryElement>();
            state.RequireForUpdate<InventoryMoveRPC>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            Entity registryEntity = SystemAPI.GetSingletonEntity<ItemRegistryElement>();
            DynamicBuffer<ItemRegistryElement> registry = SystemAPI.GetBuffer<ItemRegistryElement>(registryEntity);
            DynamicBuffer<TurretSpecElement> specs = SystemAPI.GetBuffer<TurretSpecElement>(registryEntity);

            // NetworkId -> 우주선
            var ships = new NativeHashMap<int, Entity>(4, Allocator.Temp);

            foreach (
                var (owner, ship) in
                SystemAPI.Query<RefRO<GhostOwner>>()
                    .WithAll<InventorySlotElement>()
                    .WithEntityAccess()
            ) {
                ships[owner.ValueRO.NetworkId] = ship;
            }

            var ecb = new EntityCommandBuffer(Allocator.Temp);

            foreach (
                var (request, rpc, rpcEntity) in
                SystemAPI.Query<RefRO<ReceiveRpcCommandRequest>, RefRO<InventoryMoveRPC>>()
                    .WithEntityAccess()
            ) {
                int networkId = SystemAPI.GetComponent<NetworkId>(request.ValueRO.SourceConnection).Value;

                // 파괴된 뒤 도착한 요청은 버림.
                if (ships.TryGetValue(networkId, out Entity ship))
                {
                    DynamicBuffer<InventorySlotElement> slots = SystemAPI.GetBuffer<InventorySlotElement>(ship);
                    int from = rpc.ValueRO.From;
                    int to = rpc.ValueRO.To;

                    if (Inventory.Move(slots, registry, from, to, rpc.ValueRO.Count)
                        && (slots[from].Type == SlotType.Turret || slots[to].Type == SlotType.Turret))
                    {
                        TurretEquip.Refresh(
                            slots,
                            registry,
                            specs,
                            ref SystemAPI.GetComponentRW<EquippedTurretComponent>(ship).ValueRW,
                            SystemAPI.GetBuffer<TurretElement>(ship)
                        );
                    }
                }

                ecb.DestroyEntity(rpcEntity);
            }

            ecb.Playback(state.EntityManager);
            ecb.Dispose();
            ships.Dispose();
        }
    }
}
