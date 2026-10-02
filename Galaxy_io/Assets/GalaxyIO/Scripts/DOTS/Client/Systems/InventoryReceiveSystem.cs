using GalaxyIO.DOTS.Client.Components.Singleton;
using GalaxyIO.DOTS.Components.Message;
using GalaxyIO.DOTS.Components.Singleton;
using GalaxyIO.DOTS.Inventory;
using Unity.Collections;
using Unity.Entities;
using Unity.NetCode;

namespace GalaxyIO.DOTS.Client.Systems
{
    /// <summary>
    /// <see cref="LocalInventoryComponent"/> 싱글톤을 만들고, 서버가 보낸 인벤토리 RPC 를 사본에 반영하는 시스템.
    /// </summary>
    [WorldSystemFilter(WorldSystemFilterFlags.ClientSimulation)]
    public partial struct InventoryReceiveSystem : ISystem
    {
        public void OnCreate(ref SystemState state)
        {
            state.EntityManager.AddComponentObject(state.EntityManager.CreateEntity(), new LocalInventoryComponent());
            state.RequireForUpdate<ItemConfigComponent>();
            state.RequireAnyForUpdate(
                SystemAPI.QueryBuilder().WithAll<InventoryResetRPC, ReceiveRpcCommandRequest>().Build(),
                SystemAPI.QueryBuilder().WithAll<InventorySlotRPC, ReceiveRpcCommandRequest>().Build()
            );
        }

        public void OnUpdate(ref SystemState state)
        {
            LocalInventoryComponent local = SystemAPI.ManagedAPI.GetSingleton<LocalInventoryComponent>();

            var ecb = new EntityCommandBuffer(Allocator.Temp);

            // 칸 RPC 보다 먼저 처리함. 서버도 초기화를 먼저 보냄.
            foreach (
                var (_, rpcEntity) in
                SystemAPI.Query<RefRO<ReceiveRpcCommandRequest>>()
                    .WithAll<InventoryResetRPC>()
                    .WithEntityAccess()
            ) {
                local.Value = new PlayerInventory(SystemAPI.GetSingleton<ItemConfigComponent>());
                ecb.DestroyEntity(rpcEntity);
            }

            foreach (
                var (rpc, rpcEntity) in
                SystemAPI.Query<RefRO<InventorySlotRPC>>()
                    .WithAll<ReceiveRpcCommandRequest>()
                    .WithEntityAccess()
            ) {
                if (local.Value != null && rpc.ValueRO.Slot < local.Value.Length)
                {
                    local.Value.Set(rpc.ValueRO.Slot, rpc.ValueRO.Id, rpc.ValueRO.Count);
                }

                ecb.DestroyEntity(rpcEntity);
            }

            ecb.Playback(state.EntityManager);
            ecb.Dispose();
        }
    }
}
