using GalaxyIO.DOTS.Client.Components.Singleton;
using GalaxyIO.DOTS.Components.Actor;
using GalaxyIO.DOTS.Components.Message;
using GalaxyIO.DOTS.Components.Singleton;
using GalaxyIO.DOTS.Inventory;
using Unity.Entities;
using Unity.NetCode;

namespace GalaxyIO.DOTS.Client.Systems
{
    /// <summary>
    /// 포탑 칸을 건드리는 <see cref="InventoryMoveRPC"/>를 보낼 때 로컬 우주선의 총구 쿨다운을 미리 채우는 시스템.
    /// 서버도 포탑을 바꿀 때 같은 쿨다운을 채우므로 예측 발사가 서버와 어긋나지 않음.
    /// </summary>
    [WorldSystemFilter(WorldSystemFilterFlags.ClientSimulation)]
    public partial struct TurretCooldownResetSystem : ISystem
    {
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<ItemRegistryElement>();
            state.RequireForUpdate(
                SystemAPI.QueryBuilder().WithAll<InventoryMoveRPC, SendRpcCommandRequest>().Build()
            );
        }

        public void OnUpdate(ref SystemState state)
        {
            PlayerInventory inventory = SystemAPI.ManagedAPI.GetSingleton<LocalInventoryComponent>().Value;

            Entity registryEntity = SystemAPI.GetSingletonEntity<ItemRegistryElement>();
            DynamicBuffer<ItemRegistryElement> registry = SystemAPI.GetBuffer<ItemRegistryElement>(registryEntity);
            DynamicBuffer<TurretSpecElement> specs = SystemAPI.GetBuffer<TurretSpecElement>(registryEntity);

            Entity ship = Entity.Null;

            foreach (
                var (_, entity) in
                SystemAPI.Query<RefRO<GhostOwnerIsLocal>>()
                    .WithAll<PlayerNameComponent, TurretElement>()
                    .WithEntityAccess()
            ) {
                ship = entity;
            }

            if (inventory == null || ship == Entity.Null)
            {
                return;
            }

            // RPC 는 이 프레임 마지막에 전송되며 사라짐. 여기서는 읽기만 함.
            foreach (
                var rpc in
                SystemAPI.Query<RefRO<InventoryMoveRPC>>().WithAll<SendRpcCommandRequest>()
            ) {
                int from = rpc.ValueRO.From;
                int to = rpc.ValueRO.To;

                if (from >= inventory.Length || to >= inventory.Length)
                {
                    continue;
                }

                // 옮긴 뒤 포탑 칸에 올 아이템. 포탑은 한 개씩만 쌓이므로 통째로 옮기거나 맞바꿈.
                ItemId turret;

                if (inventory[to].Type == SlotType.Turret)
                {
                    turret = inventory[from].Id;
                }
                else if (inventory[from].Type == SlotType.Turret)
                {
                    turret = inventory[to].Id;
                }
                else
                {
                    continue;
                }

                TurretEquip.ResetCooldown(SystemAPI.GetBuffer<TurretElement>(ship), registry, specs, turret);
            }
        }
    }
}
