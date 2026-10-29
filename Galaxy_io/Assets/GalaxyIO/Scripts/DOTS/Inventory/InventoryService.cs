using System.Collections.Generic;
using GalaxyIO.DOTS.Authoring;
using GalaxyIO.DOTS.Authoring.Config;
using GalaxyIO.DOTS.Components.Actor;
using GalaxyIO.DOTS.Components.Message;
using GalaxyIO.DOTS.Components.Singleton;
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
        private readonly EntityQuery spaceshipRegistryQuery;
        private readonly EntityQuery configQuery;
        private readonly EntityQuery prefabQuery;
        private readonly EntityQuery shipQuery;
        private readonly EntityQuery craftQuery;
        private readonly EntityQuery connectionQuery;
        private readonly EntityQuery spawnQuery;

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
                ComponentType.ReadWrite<ItemRegistryElement>()
            );
            spaceshipRegistryQuery = entityManager.CreateEntityQuery(
                ComponentType.ReadOnly<SpaceshipRegistryAssetComponent>(),
                ComponentType.ReadOnly<SpaceshipRegistryElement>()
            );
            configQuery = entityManager.CreateEntityQuery(ComponentType.ReadOnly<ItemConfigComponent>());
            prefabQuery = entityManager.CreateEntityQuery(ComponentType.ReadOnly<PrefabRegistryComponent>());
            shipQuery = entityManager.CreateEntityQuery(
                ComponentType.ReadOnly<GhostOwner>(),
                ComponentType.ReadOnly<PlayerNameComponent>(),
                ComponentType.ReadOnly<SpaceshipSpecComponent>(),
                ComponentType.ReadOnly<LocalTransform>(),
                ComponentType.ReadWrite<EquippedTurretComponent>()
            );
            craftQuery = entityManager.CreateEntityQuery(
                ComponentType.ReadOnly<CarriedCraftComponent>(),
                ComponentType.ReadOnly<SpaceshipSpecComponent>(),
                ComponentType.ReadOnly<LocalTransform>(),
                ComponentType.ReadWrite<CarriedItemElement>()
            );
            connectionQuery = entityManager.CreateEntityQuery(ComponentType.ReadOnly<NetworkId>());
            spawnQuery = entityManager.CreateEntityQuery(ComponentType.ReadWrite<ItemSpawnMessage>());
        }

        private ItemRegistryAsset Items => registryQuery.GetSingleton<ItemRegistryAssetComponent>().Value;

        /// <summary>
        /// 넣은 개수를 반환. 인벤토리가 없거나 재료가 아니면 0.
        /// </summary>
        public int Insert(int networkId, ItemId id, int count)
        {
            return inventories.TryGetValue(networkId, out PlayerInventory inventory) ? inventory.Insert(id, count) : 0;
        }

        /// <summary>
        /// 파워 셀을 소모해 우주선의 능력치를 한 단계 올림.
        /// 최대 단계거나 파워 셀이 모자라면 아무것도 안 함.
        /// </summary>
        public void Upgrade(int networkId, UpgradeRPC rpc)
        {
            Entity ship = FindShip(networkId);
            int stat = (int)rpc.Stat;

            if (ship == Entity.Null
                || !inventories.TryGetValue(networkId, out PlayerInventory inventory)
                || !entityManager.HasBuffer<UpgradeElement>(ship)
                || stat >= entityManager.GetBuffer<UpgradeElement>(ship, true).Length)
            {
                return;
            }

            int level = entityManager.GetBuffer<UpgradeLevelElement>(ship, true)[stat].Value;
            int cost = Components.Actor.Upgrade.Cost(level);

            if (level >= entityManager.GetBuffer<UpgradeElement>(ship, true)[stat].MaxLevel
                || inventory.Count(ItemId.PowerCell) < cost)
            {
                return;
            }

            inventory.Set(ItemId.PowerCell, inventory.Count(ItemId.PowerCell) - cost);
            entityManager.GetBuffer<UpgradeLevelElement>(ship).ElementAt(stat).Value = (byte)(level + 1);
        }

        /// <summary>
        /// 포탑 칸에 포탑을 장착함. 조종 포탑 칸은 잠금 해제하지 않은 포탑만 제작법의 재료를 소모해 잠금 해제부터 함.
        /// 자동 포탑 칸은 잠금 해제와 관계없이 바꿀 때마다 재료를 소모함.
        /// 없는 칸이거나 그 칸에 이미 장착한 포탑이거나 재료가 모자라면 아무것도 안 함.
        /// </summary>
        public void EquipTurret(int networkId, TurretEquipRPC rpc)
        {
            Entity ship = FindShip(networkId);
            RecipeAsset recipe = Items.FindRecipe(rpc.Turret);

            if (ship == Entity.Null
                || recipe == null
                || !inventories.TryGetValue(networkId, out PlayerInventory inventory))
            {
                return;
            }

            // 음수면 조종 포탑 칸.
            int auto = TurretEquip.AutoTurretIndex(rpc.Slot, TurretEquip.HasMainTurretSlot(entityManager, ship));
            DynamicBuffer<AutoTurretElement> autoTurrets = entityManager.GetBuffer<AutoTurretElement>(ship, true);

            if (auto >= autoTurrets.Length)
            {
                return;
            }

            ItemId current = auto < 0
                ? entityManager.GetComponentData<EquippedTurretComponent>(ship).Value
                : autoTurrets[auto].Turret;

            if (current == rpc.Turret)
            {
                return;
            }

            Entity registryEntity = registryQuery.GetSingletonEntity();
            ItemRegistryElement element = entityManager.GetBuffer<ItemRegistryElement>(registryEntity, true).Get(rpc.Turret);

            if (auto >= 0)
            {
                if (!inventory.Consume(recipe.Ingredients))
                {
                    return;
                }

                // 발사 간격만큼 기다렸다가 사격부터 시작함.
                ref AutoTurretElement autoTurret = ref entityManager.GetBuffer<AutoTurretElement>(ship).ElementAt(auto);
                autoTurret.Turret = rpc.Turret;
                autoTurret.Firing = false;

                ref AutoTurretMountElement mount = ref entityManager.GetBuffer<AutoTurretMountElement>(ship).ElementAt(auto);
                mount.Cooldown = element.Turret.MaxFireCooldown;
                mount.Resting = false;
                mount.CycleRemaining = element.AutoTurret.FireDuration;

                return;
            }

            if (!inventory.IsUnlocked(rpc.Turret))
            {
                if (!inventory.Consume(recipe.Ingredients))
                {
                    return;
                }

                inventory.Unlock(rpc.Turret);
            }

            EquippedTurretComponent equipped = entityManager.GetComponentData<EquippedTurretComponent>(ship);

            TurretEquip.Refresh(
                rpc.Turret,
                entityManager.GetBuffer<ItemRegistryElement>(registryEntity),
                ref equipped,
                entityManager.GetBuffer<TurretCooldownElement>(ship)
            );

            entityManager.SetComponentData(ship, equipped);
        }

        /// <summary>
        /// 재료를 소모해 같은 자리에서 다른 기종으로 바꿔 탐. 장착한 포탑은 그대로 옮기고(고정 포탑 제외) 강화는 처음부터.
        /// 자동 포탑은 옮기지 않음. 새 기종의 자동 포탑 칸은 비어 있음.
        /// 이미 탄 기종이거나 프리팹이 없거나 재료가 모자라면 아무것도 안 함.
        /// </summary>
        public void PurchaseSpaceship(int networkId, SpaceshipPurchaseRPC rpc)
        {
            Entity ship = FindShip(networkId);

            if (ship == Entity.Null || spaceshipRegistryQuery.IsEmpty)
            {
                return;
            }

            Entity prefab = spaceshipRegistryQuery.GetSingletonBuffer<SpaceshipRegistryElement>(true).Find(rpc.Spaceship);
            SpaceshipAsset spaceship = spaceshipRegistryQuery.GetSingleton<SpaceshipRegistryAssetComponent>().Value.Find(rpc.Spaceship);

            if (prefab == Entity.Null
                || spaceship == null
                || !inventories.TryGetValue(networkId, out PlayerInventory inventory)
                || entityManager.GetComponentData<SpaceshipKindComponent>(ship).Value == rpc.Spaceship
                || !inventory.Consume(spaceship.Ingredients))
            {
                return;
            }

            LocalTransform oldTransform = entityManager.GetComponentData<LocalTransform>(ship);
            ItemId turret = entityManager.GetComponentData<EquippedTurretComponent>(ship).Value;
            PlayerNameComponent playerName = entityManager.GetComponentData<PlayerNameComponent>(ship);

            // 크기는 프리팹 값, 위치와 회전은 타던 우주선 값.
            LocalTransform transform = entityManager.GetComponentData<LocalTransform>(prefab);
            transform.Position = oldTransform.Position;
            transform.Rotation = oldTransform.Rotation;

            Entity newShip = entityManager.Instantiate(prefab);
            entityManager.SetComponentData(newShip, new GhostOwner { NetworkId = networkId });
            entityManager.SetComponentData(newShip, playerName);
            entityManager.SetComponentData(newShip, transform);

            // 고정 포탑은 옮기지 않음. 어느 한쪽이라도 고정 포탑이면 새 기종은 프리팹의 포탑을 씀.
            if (!entityManager.HasComponent<FixedTurretTag>(ship) && !entityManager.HasComponent<FixedTurretTag>(newShip))
            {
                Entity registryEntity = registryQuery.GetSingletonEntity();
                EquippedTurretComponent equipped = entityManager.GetComponentData<EquippedTurretComponent>(newShip);

                TurretEquip.Refresh(
                    turret,
                    entityManager.GetBuffer<ItemRegistryElement>(registryEntity),
                    ref equipped,
                    entityManager.GetBuffer<TurretCooldownElement>(newShip)
                );

                entityManager.SetComponentData(newShip, equipped);
            }

            // 남은 함재기는 모선을 잃고 터짐. 기종 변경으로 터질 때는 화물을 떨어뜨리지 않음.
            using (NativeArray<Entity> crafts = craftQuery.ToEntityArray(Allocator.Temp))
            {
                foreach (Entity craft in crafts)
                {
                    if (entityManager.GetComponentData<CarriedCraftComponent>(craft).Carrier == ship)
                    {
                        entityManager.GetBuffer<CarriedItemElement>(craft).Clear();
                    }
                }
            }

            // 파괴 연출 없이 지움. 인벤토리는 NetworkId 로 이어짐.
            entityManager.DestroyEntity(ship);
        }

        /// <summary>
        /// 서버 틱마다 호출. 인벤토리 생성, 사망 드랍(함재기 포함), 접속 종료 정리, 바뀐 개수 전송을 처리함.
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

                    // 새 우주선은 프리팹에 장착된 포탑을 잠금 해제한 채로 시작함.
                    if (!inventories.ContainsKey(networkId))
                    {
                        var inventory = new PlayerInventory();
                        inventory.Unlock(entityManager.GetComponentData<EquippedTurretComponent>(ship).Value);
                        inventories[networkId] = inventory;
                        created.Add(networkId);
                    }

                    if (entityManager.GetComponentData<SpaceshipSpecComponent>(ship).Health > 0.0F)
                    {
                        continue;
                    }

                    // 사망: 떨어뜨리지 않은 재료와 잠금 해제한 포탑은 인벤토리와 함께 사라짐.
                    PlayerInventory dead = inventories[networkId];
                    float3 position = entityManager.GetComponentData<LocalTransform>(ship).Position;
                    inventories.Remove(networkId);

                    foreach (ItemId id in PlayerInventory.Ingredients)
                    {
                        Drop(id, dead.Count(id), position, config);
                    }
                }
            }

            // 격추된 함재기의 화물도 같은 규칙으로 떨어뜨림. 터지기 전까지 남아 있으므로 비워서 한 번만 떨어뜨림.
            using (NativeArray<Entity> crafts = craftQuery.ToEntityArray(Allocator.Temp))
            {
                foreach (Entity craft in crafts)
                {
                    if (entityManager.GetComponentData<SpaceshipSpecComponent>(craft).Health > 0.0F)
                    {
                        continue;
                    }

                    float3 position = entityManager.GetComponentData<LocalTransform>(craft).Position;

                    using NativeArray<CarriedItemElement> cargo = entityManager.GetBuffer<CarriedItemElement>(craft).ToNativeArray(Allocator.Temp);
                    entityManager.GetBuffer<CarriedItemElement>(craft).Clear();

                    foreach (CarriedItemElement carried in cargo)
                    {
                        Drop(carried.Id, carried.Count, position, config);
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

                foreach (ItemId id in PlayerInventory.Ingredients)
                {
                    if (inventory.TakeDirty(id))
                    {
                        SendRpc(new InventoryCountRPC
                        {
                            Id    = id,
                            Count = (ushort)inventory.Count(id),
                        }, connection);
                    }
                }

                while (inventory.TakeUnlock(out ItemId turret))
                {
                    SendRpc(new TurretUnlockRPC { Turret = turret }, connection);
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
        /// 사망 드랍. 재료 종류마다 떨어뜨릴지 굴리고, 떨어뜨릴 재료는 하나하나 남길지 다시 굴림.
        /// </summary>
        private void Drop(ItemId id, int owned, float3 position, in ItemConfigComponent config)
        {
            if (owned == 0 || random.NextFloat() >= config.PlayerKindDropChance)
            {
                return;
            }

            int count = 0;

            for (int n = 0; n < owned; n++)
            {
                if (random.NextFloat() < config.PlayerStackDropChance)
                {
                    count++;
                }
            }

            if (count > 0)
            {
                Spawn(id, count, position, random.NextFloat2Direction() * random.NextFloat(0.5F, 1.0F));
            }
        }

        /// <summary>
        /// 아이템 스폰 메시지를 남김. direction 크기만큼 미끄러지는 속력을 줌.
        /// 레지스트리에 아이템 Entity 가 없으면 아무것도 안 함.
        /// </summary>
        private void Spawn(ItemId id, int count, float3 position, float2 direction)
        {
            Entity prefab = prefabQuery.GetSingleton<PrefabRegistryComponent>().ItemEntity;

            if (prefab == Entity.Null)
            {
                return;
            }

            // 미끄러지는 속력과 줍기 대기 시간은 프리팹 값을 씀.
            ItemComponent item = entityManager.GetComponentData<ItemComponent>(prefab);

            spawnQuery.GetSingletonBuffer<ItemSpawnMessage>().Add(new ItemSpawnMessage
            {
                Prefab      = prefab,
                Id          = id,
                Count       = count,
                Position    = position,
                Velocity    = direction * item.SlideSpeed,
                PickupDelay = item.DropPickupDelay,
            });
        }
    }
}