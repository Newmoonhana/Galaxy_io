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
    /// <see cref="CraftRPC"/>를 받아 설계도 한 장과 재료를 소모하고 결과물을 인벤토리에 넣는 시스템.
    /// 재료가 모자라거나 결과물을 넣을 칸이 없으면 아무것도 안 함.
    /// </summary>
    [BurstCompile]
    [WorldSystemFilter(WorldSystemFilterFlags.ServerSimulation)]
    public partial struct CraftSystem : ISystem
    {
        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<ItemRegistryElement>();
            state.RequireForUpdate<CraftRPC>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            Entity registryEntity = SystemAPI.GetSingletonEntity<ItemRegistryElement>();
            DynamicBuffer<ItemRegistryElement> registry = SystemAPI.GetBuffer<ItemRegistryElement>(registryEntity);
            DynamicBuffer<RecipeIngredientElement> ingredients = SystemAPI.GetBuffer<RecipeIngredientElement>(registryEntity);

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
                SystemAPI.Query<RefRO<ReceiveRpcCommandRequest>, RefRO<CraftRPC>>()
                    .WithEntityAccess()
            ) {
                ecb.DestroyEntity(rpcEntity);

                int networkId = SystemAPI.GetComponent<NetworkId>(request.ValueRO.SourceConnection).Value;
                int index = rpc.ValueRO.Slot;

                // 파괴된 뒤 도착한 요청은 버림.
                if (!ships.TryGetValue(networkId, out Entity ship))
                {
                    continue;
                }

                DynamicBuffer<InventorySlotElement> slots = SystemAPI.GetBuffer<InventorySlotElement>(ship);

                if (index >= slots.Length || !registry.Contains(slots[index].Id))
                {
                    continue;
                }

                ItemRegistryElement blueprint = registry[(int)slots[index].Id];

                if (!registry.Contains(blueprint.CraftResult))
                {
                    continue;
                }

                bool enough = true;

                for (int i = 0; i < blueprint.IngredientCount && enough; i++)
                {
                    RecipeIngredientElement ingredient = ingredients[blueprint.IngredientStart + i];
                    enough = Inventory.Count(slots, ingredient.Id) >= ingredient.Count;
                }

                if (!enough)
                {
                    continue;
                }

                // 결과물을 넣을 칸이 없으면 되돌림. 소모한 뒤 빈 칸이 생길 수 있으므로 먼저 소모해 봄.
                NativeArray<InventorySlotElement> backup = slots.ToNativeArray(Allocator.Temp);

                InventorySlotElement slot = slots[index];
                slot.Count--;

                if (slot.Count == 0)
                {
                    slot.Id = ItemId.None;
                }

                slots[index] = slot;

                for (int i = 0; i < blueprint.IngredientCount; i++)
                {
                    RecipeIngredientElement ingredient = ingredients[blueprint.IngredientStart + i];
                    Inventory.Remove(slots, ingredient.Id, ingredient.Count);
                }

                if (Inventory.Insert(slots, registry, blueprint.CraftResult, 1) == 0)
                {
                    slots.CopyFrom(backup);
                }

                backup.Dispose();
            }

            ecb.Playback(state.EntityManager);
            ecb.Dispose();
            ships.Dispose();
        }
    }
}
