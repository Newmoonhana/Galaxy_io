using System;
using GalaxyIO.DOTS.Components.Message;
using GalaxyIO.DOTS.Components.Singleton;
using GalaxyIO.DOTS.Inventory;
using Unity.Collections;
using Unity.Entities;
using Unity.NetCode;

namespace GalaxyIO.DOTS.Systems
{
    /// <summary>
    /// <see cref="InventoryService"/>와 ECS 를 잇는 시스템. 받은 인벤토리 RPC 를 서비스에 넘기고 서비스를 틱마다 갱신함.
    /// 사망 드랍을 위해 우주선이 파괴되기 전에 실행함.
    /// </summary>
    [WorldSystemFilter(WorldSystemFilterFlags.ServerSimulation)]
    [UpdateInGroup(typeof(EventSystemGroup))]
    [UpdateAfter(typeof(DamageNotifySystem))]
    [UpdateBefore(typeof(SpaceshipDeathSystem))]
    public partial struct InventorySystem : ISystem
    {
        private EntityQuery upgradeQuery;
        private EntityQuery turretQuery;
        private EntityQuery spaceshipQuery;

        public void OnCreate(ref SystemState state)
        {
            Entity entity = state.EntityManager.CreateEntity();
            state.EntityManager.AddComponentObject(entity, new InventoryServiceComponent
            {
                Value = new InventoryService(state.EntityManager),
            });

            upgradeQuery = SystemAPI.QueryBuilder().WithAll<ReceiveRpcCommandRequest, UpgradeRPC>().Build();
            turretQuery = SystemAPI.QueryBuilder().WithAll<ReceiveRpcCommandRequest, TurretEquipRPC>().Build();
            spaceshipQuery = SystemAPI.QueryBuilder().WithAll<ReceiveRpcCommandRequest, SpaceshipPurchaseRPC>().Build();

            state.RequireForUpdate<ItemRegistryAssetComponent>();
            state.RequireForUpdate<ItemConfigComponent>();
            state.RequireForUpdate<PrefabRegistryComponent>();
        }

        public void OnUpdate(ref SystemState state)
        {
            InventoryService service = SystemAPI.ManagedAPI.GetSingleton<InventoryServiceComponent>().Value;

            if (!upgradeQuery.IsEmpty) Receive<UpgradeRPC>(state.EntityManager, upgradeQuery, service.Upgrade);
            if (!turretQuery.IsEmpty) Receive<TurretEquipRPC>(state.EntityManager, turretQuery, service.EquipTurret);
            if (!spaceshipQuery.IsEmpty) Receive<SpaceshipPurchaseRPC>(state.EntityManager, spaceshipQuery, service.PurchaseSpaceship);

            service.Update();
        }

        // 받은 RPC 를 모두 꺼내 보낸 쪽 NetworkId 와 함께 넘김. RPC Entity 는 먼저 지움.
        private static void Receive<T>(EntityManager entityManager, EntityQuery query, Action<int, T> handle)
            where T : unmanaged, IComponentData
        {
            using NativeArray<ReceiveRpcCommandRequest> requests = query.ToComponentDataArray<ReceiveRpcCommandRequest>(Allocator.Temp);
            using NativeArray<T> rpcs = query.ToComponentDataArray<T>(Allocator.Temp);

            entityManager.DestroyEntity(query);

            for (int i = 0; i < rpcs.Length; i++)
            {
                // 보낸 뒤 접속이 끊긴 요청은 버림.
                if (entityManager.HasComponent<NetworkId>(requests[i].SourceConnection))
                {
                    handle(entityManager.GetComponentData<NetworkId>(requests[i].SourceConnection).Value, rpcs[i]);
                }
            }
        }
    }
}
