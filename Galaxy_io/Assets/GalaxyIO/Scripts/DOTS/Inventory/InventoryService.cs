using System.Collections.Generic;
using GalaxyIO.DOTS.Authoring.Config;
using GalaxyIO.DOTS.Components.Actor;
using GalaxyIO.DOTS.Components.Message;
using GalaxyIO.DOTS.Components.Singleton;
using GalaxyIO.DOTS.Spaceship;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.NetCode;
using Unity.Transforms;
using Random = Unity.Mathematics.Random;

namespace GalaxyIO.DOTS.Inventory
{
    /// <summary>
    /// 서버의 플레이어 인벤토리 원본과 그 처리. 서버 월드당 한 개.
    /// 살아 있는 플레이어 우주선의 주인만 인벤토리를 가짐.
    /// </summary>
    public sealed class InventoryService
    {
        private readonly EntityManager entityManager;
        private readonly EntityQuery registryQuery;
        private readonly EntityQuery configQuery;
        private readonly EntityQuery prefabQuery;
        private readonly EntityQuery shipQuery;
        private readonly EntityQuery connectionQuery;

        // NetworkId -> 인벤토리
        private readonly Dictionary<int, PlayerInventory> inventories = new Dictionary<int, PlayerInventory>();

        // 새로 만든 인벤토리. 주인의 사본을 먼저 비우게 함.
        private readonly HashSet<int> created = new HashSet<int>();

        private Random random = Random.CreateFromIndex((uint)System.Environment.TickCount & 0x7FFFFFFFu);

        public InventoryService(EntityManager entityManager)
        {
            this.entityManager = entityManager;

            registryQuery = entityManager.CreateEntityQuery(
                ComponentType.ReadOnly<ItemRegistryAssetComponent>(),
                ComponentType.ReadWrite<ItemRegistryElement>(),
                ComponentType.ReadWrite<TurretSpecElement>()
            );
            configQuery = entityManager.CreateEntityQuery(ComponentType.ReadOnly<ItemConfigComponent>());
            prefabQuery = entityManager.CreateEntityQuery(ComponentType.ReadOnly<PrefabRegistryElement>());
            shipQuery = entityManager.CreateEntityQuery(
                ComponentType.ReadOnly<GhostOwner>(),
                ComponentType.ReadOnly<PlayerNameComponent>(),
                ComponentType.ReadOnly<SpaceshipSpecComponent>(),
                ComponentType.ReadOnly<LocalTransform>(),
                ComponentType.ReadWrite<EquippedTurretComponent>()
            );
            connectionQuery = entityManager.CreateEntityQuery(ComponentType.ReadOnly<NetworkId>());
        }

        private ItemRegistryAsset Items => registryQuery.GetSingleton<ItemRegistryAssetComponent>().Value;

        /// <summary>
        /// 넣은 개수를 반환. 인벤토리가 없으면 0.
        /// </summary>
        public int Insert(int networkId, ItemId id, int count)
        {
            return inventories.TryGetValue(networkId, out PlayerInventory inventory) ? inventory.Insert(Items, id, count) : 0;
        }

        public void Move(int networkId, InventoryMoveRPC rpc)
        {
            if (!inventories.TryGetValue(networkId, out PlayerInventory inventory)
                || !inventory.Move(Items, rpc.From, rpc.To, rpc.Count))
            {
                return;
            }

            if (inventory[rpc.From].Type == SlotType.Turret || inventory[rpc.To].Type == SlotType.Turret)
            {
                RefreshTurret(networkId, inventory);
            }
        }

        /// <summary>
        /// 한 칸에서 아이템을 꺼내 기수 방향으로 던짐. 포탑 칸에서 던지면 장착 포탑도 뺌.
        /// </summary>
        public void Drop(int networkId, InventoryDropRPC rpc)
        {
            Entity ship = FindShip(networkId);

            if (ship == Entity.Null
                || !inventories.TryGetValue(networkId, out PlayerInventory inventory)
                || rpc.Slot >= inventory.Length
                || inventory[rpc.Slot].Id == ItemId.None)
            {
                return;
            }

            ItemSlot slot = inventory[rpc.Slot];
            int count = math.min(rpc.Count, slot.Count);
            LocalTransform shipTransform = entityManager.GetComponentData<LocalTransform>(ship);

            if (!Spawn(slot.Id, count, shipTransform.Position, SpaceshipHeading.FromRotation(shipTransform.Rotation), true))
            {
                return;
            }

            inventory.Set(rpc.Slot, slot.Id, slot.Count - count);

            if (slot.Type == SlotType.Turret)
            {
                RefreshTurret(networkId, inventory);
            }
        }

        public void Craft(int networkId, CraftRPC rpc)
        {
            if (inventories.TryGetValue(networkId, out PlayerInventory inventory))
            {
                inventory.Craft(Items, rpc.Slot);
            }
        }

        /// <summary>
        /// 서버 틱마다 호출. 인벤토리 생성, 사망 드랍, 접속 종료 정리, 바뀐 칸 전송을 처리함.
        /// 우주선이 파괴되기 전에 불러야 사망 드랍이 동작함.
        /// </summary>
        public void Update()
        {
            ItemConfigComponent config = configQuery.GetSingleton<ItemConfigComponent>();

            using (NativeArray<Entity> ships = shipQuery.ToEntityArray(Allocator.Temp))
            {
                foreach (Entity ship in ships)
                {
                    int networkId = entityManager.GetComponentData<GhostOwner>(ship).NetworkId;

                    // 새 우주선은 프리팹에 장착된 포탑을 첫 포탑 칸에 넣은 채로 시작함.
                    if (!inventories.ContainsKey(networkId))
                    {
                        ItemId turret = entityManager.GetComponentData<EquippedTurretComponent>(ship).Value;
                        inventories[networkId] = new PlayerInventory(config, turret);
                        created.Add(networkId);
                    }

                    if (entityManager.GetComponentData<SpaceshipSpecComponent>(ship).Health > 0.0F)
                    {
                        continue;
                    }

                    // 사망: 칸마다 떨어뜨릴지 굴리고, 떨어뜨릴 칸은 아이템 하나하나 남길지 다시 굴림.
                    // 장착한 포탑(포탑 칸)은 떨어뜨리지 않음. 남은 아이템은 인벤토리와 함께 사라짐.
                    PlayerInventory inventory = inventories[networkId];
                    float3 position = entityManager.GetComponentData<LocalTransform>(ship).Position;
                    inventories.Remove(networkId);

                    for (int i = 0; i < inventory.Length; i++)
                    {
                        ItemSlot slot = inventory[i];

                        if (slot.Id == ItemId.None
                            || slot.Type == SlotType.Turret
                            || random.NextFloat() >= config.PlayerSlotDropChance)
                        {
                            continue;
                        }

                        int count = 0;

                        for (int n = 0; n < slot.Count; n++)
                        {
                            if (random.NextFloat() < config.PlayerStackDropChance)
                            {
                                count++;
                            }
                        }

                        if (count > 0)
                        {
                            Spawn(slot.Id, count, position, random.NextFloat2Direction() * random.NextFloat(0.5F, 1.0F), false);
                        }
                    }
                }
            }

            // NetworkId -> 접속
            var connections = new Dictionary<int, Entity>();

            using (NativeArray<Entity> entities = connectionQuery.ToEntityArray(Allocator.Temp))
            {
                foreach (Entity connection in entities)
                {
                    connections[entityManager.GetComponentData<NetworkId>(connection).Value] = connection;
                }
            }

            // 접속이 끊긴 플레이어의 인벤토리는 버림.
            var left = new List<int>();

            foreach (var (networkId, inventory) in inventories)
            {
                if (!connections.TryGetValue(networkId, out Entity connection))
                {
                    left.Add(networkId);
                    continue;
                }

                if (created.Remove(networkId))
                {
                    SendRpc(new InventoryResetRPC(), connection);
                }

                for (int i = 0; i < inventory.Length; i++)
                {
                    if (inventory.TakeDirty(i))
                    {
                        SendRpc(new InventorySlotRPC
                        {
                            Slot  = (byte)i,
                            Id    = inventory[i].Id,
                            Count = (byte)inventory[i].Count,
                        }, connection);
                    }
                }
            }

            foreach (int networkId in left)
            {
                inventories.Remove(networkId);
                created.Remove(networkId);
            }
        }

        private void SendRpc<T>(T rpc, Entity connection) where T : unmanaged, IRpcCommand
        {
            Entity entity = entityManager.CreateEntity();
            entityManager.AddComponentData(entity, rpc);
            entityManager.AddComponentData(entity, new SendRpcCommandRequest { TargetConnection = connection });
        }

        // 장착 포탑이 달라졌으면 바꾸고 쿨다운을 채움.
        private void RefreshTurret(int networkId, PlayerInventory inventory)
        {
            Entity ship = FindShip(networkId);

            if (ship == Entity.Null)
            {
                return;
            }

            Entity registryEntity = registryQuery.GetSingletonEntity();
            EquippedTurretComponent equipped = entityManager.GetComponentData<EquippedTurretComponent>(ship);

            TurretEquip.Refresh(
                inventory.EquippedTurret(Items),
                entityManager.GetBuffer<ItemRegistryElement>(registryEntity),
                entityManager.GetBuffer<TurretSpecElement>(registryEntity),
                ref equipped,
                entityManager.GetBuffer<TurretCooldownElement>(ship)
            );

            entityManager.SetComponentData(ship, equipped);
        }

        private Entity FindShip(int networkId)
        {
            using NativeArray<Entity> ships = shipQuery.ToEntityArray(Allocator.Temp);

            foreach (Entity ship in ships)
            {
                if (entityManager.GetComponentData<GhostOwner>(ship).NetworkId == networkId)
                {
                    return ship;
                }
            }

            return Entity.Null;
        }

        /// <summary>
        /// 아이템 Entity 를 스폰함. 던지면 direction 방향으로 던지는 속력, 아니면 direction 크기만큼 미끄러지는 속력을 줌.
        /// 레지스트리에 아이템 Entity 나 아이템이 없으면 false.
        /// </summary>
        private bool Spawn(ItemId id, int count, float3 position, float2 direction, bool thrown)
        {
            Entity prefab = prefabQuery.GetSingletonBuffer<PrefabRegistryElement>(true).Find(PrefabId.ItemEntity);
            DynamicBuffer<ItemRegistryElement> registry = registryQuery.GetSingletonBuffer<ItemRegistryElement>(true);

            if (prefab == Entity.Null || !registry.Contains(id))
            {
                return false;
            }

            // 프리팹의 설정값과 회전/크기는 그대로 쓰고 아이템 종류, 속도, 튕김, 위치만 바꿈.
            ItemComponent item = entityManager.GetComponentData<ItemComponent>(prefab);
            item.Id = id;
            item.Count = count;
            item.Launch(
                direction * (thrown ? item.ThrowSpeed : item.SlideSpeed),
                thrown ? item.ThrowPickupDelay : item.DropPickupDelay
            );

            ColliderComponent collider = entityManager.GetComponentData<ColliderComponent>(prefab);
            collider.Extents = registry[(int)id].HalfExtents;

            LocalTransform transform = entityManager.GetComponentData<LocalTransform>(prefab);
            transform.Position = position;

            Entity entity = entityManager.Instantiate(prefab);
            entityManager.SetComponentData(entity, item);
            entityManager.SetComponentData(entity, collider);
            entityManager.SetComponentData(entity, transform);
            return true;
        }
    }
}
