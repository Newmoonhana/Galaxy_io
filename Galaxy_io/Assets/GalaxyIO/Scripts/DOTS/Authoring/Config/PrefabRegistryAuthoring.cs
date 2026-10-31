using System;
using System.Collections.Generic;
using GalaxyIO.DOTS.Components.Blob;
using GalaxyIO.DOTS.Components.Singleton;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.NetCode;
using UnityEngine;

namespace GalaxyIO.DOTS.Authoring.Config
{
    /// <summary>
    /// <see cref="PrefabRegistryAsset"/>, <see cref="ItemRegistryAsset"/>, <see cref="SpaceshipRegistryAsset"/>을
    /// 서브씬에 베이킹하는 연결 컴포넌트. 월드당 한 개.
    /// </summary>
    public sealed class PrefabRegistryAuthoring : MonoBehaviour
    {
        [SerializeField]
        [Tooltip("베이킹할 프리팹 레지스트리")]
        public PrefabRegistryAsset Registry;

        [SerializeField]
        [Tooltip("베이킹할 아이템 레지스트리")]
        public ItemRegistryAsset ItemRegistry;

        [SerializeField]
        [Tooltip("베이킹할 우주선 레지스트리")]
        public SpaceshipRegistryAsset SpaceshipRegistry;
    }

    public sealed class PrefabRegistryBaker : Baker<PrefabRegistryAuthoring>
    {
        public override void Bake(PrefabRegistryAuthoring authoring)
        {
            // 에셋 내용이 바뀌면 다시 베이킹.
            DependsOn(authoring.Registry);

            if (authoring.Registry == null)
            {
                Debug.LogWarning("PrefabRegistry: 레지스트리 에셋이 비어 있음.", authoring);
                return;
            }

            PrefabRegistryAsset registry = authoring.Registry;

            AddComponent(GetEntity(TransformUsageFlags.None), new PrefabRegistryComponent
            {
                ExplosionEffect   = GetPrefab(registry.ExplosionEffect),
                EnemyStrafingShip = GetPrefab(registry.EnemyStrafingShip),
                ItemEntity        = GetPrefab(registry.ItemEntity),
            });
        }

        // 빈 칸은 Entity.Null.
        private Entity GetPrefab(GameObject prefab)
        {
            return prefab != null ? GetEntity(prefab, TransformUsageFlags.Dynamic) : Entity.Null;
        }
    }

    /// <summary>
    /// <see cref="PrefabRegistryAuthoring.ItemRegistry"/>를 베이킹함.
    /// </summary>
    public sealed class ItemRegistryBaker : Baker<PrefabRegistryAuthoring>
    {
        public override void Bake(PrefabRegistryAuthoring authoring)
        {
            ItemRegistryAsset itemRegistry = authoring.ItemRegistry;

            // 에셋 내용이 바뀌면 다시 베이킹.
            DependsOn(itemRegistry);

            if (itemRegistry == null)
            {
                Debug.LogWarning("ItemRegistry: 레지스트리 에셋이 비어 있음.", authoring);
                return;
            }

            Entity entity = GetEntity(TransformUsageFlags.None);
            DynamicBuffer<ItemRegistryElement> registry = AddBuffer<ItemRegistryElement>(entity);

            // 가장 큰 Id 까지 빈 칸으로 채워 둠. 칸 번호가 곧 Id.
            int maxId = 0;

            foreach (ItemAsset item in itemRegistry.Items)
            {
                DependsOn(item);

                if (item != null)
                {
                    maxId = Math.Max(maxId, (int)item.Id);
                }
            }

            registry.Resize(maxId + 1, NativeArrayOptions.ClearMemory);

            // 인벤토리, 제작은 에셋을 직접 읽음.
            AddComponentObject(entity, new ItemRegistryAssetComponent { Value = itemRegistry });

            foreach (ItemAsset item in itemRegistry.Items)
            {
                if (item == null || item.Id == ItemId.None || item.Sprite == null)
                {
                    Debug.LogWarning($"ItemRegistry: 비어 있는 항목을 건너뜀. ({item})", itemRegistry);
                    continue;
                }

                if (registry.Contains(item.Id))
                {
                    Debug.LogWarning($"ItemRegistry: Id 중복. 뒤의 항목을 버림. ({item.Id})", itemRegistry);
                    continue;
                }

                Vector3 extents = item.Sprite.bounds.extents;

                var element = new ItemRegistryElement
                {
                    Sprite      = item.Sprite,
                    HalfExtents = new float2(extents.x, extents.y),
                };

                if (item is TurretItemAsset turret)
                {
                    TurretItemAsset.TurretSpec spec = turret.Spec;

                    ProjectileReplication replication = ProjectileReplication.Ghost;

                    if (spec.Armed && spec.Projectile.GetComponent<GhostAuthoringComponent>() == null)
                    {
                        // 베이커의 GetComponent 로 읽어야 프리팹 값이 바뀔 때 다시 베이킹함.
                        ProjectileAuthoring projectile = GetComponent<ProjectileAuthoring>(spec.Projectile);
                        replication = projectile != null && projectile.SendShot
                            ? ProjectileReplication.Shot
                            : ProjectileReplication.Local;
                    }

                    element.Turret = new TurretSpecData
                    {
                        ProjectilePrefab = spec.Armed ? GetEntity(spec.Projectile, TransformUsageFlags.Dynamic) : Entity.Null,
                        Replication      = replication,
                        MaxFireCooldown  = spec.FireCooldown,
                        MuzzleDelay      = spec.MuzzleDelay,
                        ProjectileCount  = (byte)Math.Max(spec.ProjectileCount, 1),
                        Spread           = spec.Spread,
                        SpeedSpread      = spec.SpeedSpread,
                        Damage           = spec.Damage,
                        ShotSound        = spec.ShotSound,
                    };

                    TurretItemAsset.AutoTurretSpec auto = turret.AutoSpec;

                    element.AutoTurret = new AutoTurretSpecData
                    {
                        FireDuration      = auto.FireDuration,
                        RestDurationRange = new float2(auto.RestDuration.x, auto.RestDuration.y),
                        TurningSpeed      = math.radians(auto.TurningSpeed),
                        HalfSightAngle    = math.radians(auto.SightAngle) * 0.5F,
                        DetectRange       = auto.DetectRange,
                    };
                    element.SwitchTime      = turret.SwitchTime;
                    element.ReleaseFire     = turret.ReleaseFire;
                    element.ManeuverPenalty = turret.ManeuverPenalty;
                    element.Heat            = turret.Heat;
                    element.HeatMode        = turret.HeatMode;
                    element.Recoil          = turret.Recoil;
                    element.LaunchSound     = turret.LaunchSound;
                    element.LoopSound       = turret.LoopSound;
                    element.CancelSound     = turret.CancelSound;
                    element.HitSound        = turret.HitSound;
                    element.HitSoundMode    = turret.HitSoundMode;
                }

                registry[(int)item.Id] = element;
            }
        }
    }

    /// <summary>
    /// <see cref="ItemRegistryAsset.DropTables"/>를 베이킹함.
    /// </summary>
    public sealed class DropTableRegistryBaker : Baker<PrefabRegistryAuthoring>
    {
        public override void Bake(PrefabRegistryAuthoring authoring)
        {
            ItemRegistryAsset itemRegistry = authoring.ItemRegistry;

            // 에셋 내용이 바뀌면 다시 베이킹. 빈 레지스트리 경고는 ItemRegistryBaker 가 남김.
            DependsOn(itemRegistry);

            if (itemRegistry == null)
            {
                return;
            }

            Entity entity = GetEntity(TransformUsageFlags.None);
            DynamicBuffer<DropTableRegistryElement> registry = AddBuffer<DropTableRegistryElement>(entity);

            // 모든 적 종류만큼 빈 칸으로 채워 둠. 칸 번호가 곧 EnemyType.
            registry.Resize(Enum.GetValues(typeof(EnemyType)).Length, NativeArrayOptions.ClearMemory);

            foreach (DropTableAsset table in itemRegistry.DropTables)
            {
                DependsOn(table);

                if (table == null || table.Enemy == EnemyType.None)
                {
                    Debug.LogWarning($"DropTableRegistry: 적 종류가 비어 있는 항목을 건너뜀. ({table})", itemRegistry);
                    continue;
                }

                if (registry[(int)table.Enemy].Table.IsCreated)
                {
                    Debug.LogWarning($"DropTableRegistry: 적 종류 중복. 뒤의 항목을 버림. ({table.Enemy})", itemRegistry);
                    continue;
                }

                var rolls = new List<DropRoll>();

                foreach (DropTableAsset.Roll roll in table.Rolls)
                {
                    var entries = new List<DropEntryBlob>();

                    foreach (DropTableAsset.Entry entry in roll.Entries)
                    {
                        DependsOn(entry.Item);

                        if (entry.Item == null || entry.Item.Id == ItemId.None || entry.Weight <= 0)
                        {
                            Debug.LogWarning("DropTableRegistry: 아이템이 비어 있거나 가중치가 0인 항목을 건너뜀.", table);
                            continue;
                        }

                        entries.Add(new DropEntryBlob
                        {
                            ItemId   = entry.Item.Id,
                            MinCount = entry.CountRange.x,
                            MaxCount = entry.CountRange.y,
                            Weight   = entry.Weight,
                        });
                    }

                    rolls.Add(new DropRoll { Chance = roll.Chance, Entries = entries });
                }

                BlobAssetReference<DropTableBlob> blob = DropTables.Create(rolls);

                // 같은 내용의 블롭은 하나로 합쳐짐.
                AddBlobAsset(ref blob, out _);

                registry[(int)table.Enemy] = new DropTableRegistryElement { Table = blob };
            }
        }
    }

    /// <summary>
    /// <see cref="PrefabRegistryAuthoring.SpaceshipRegistry"/>를 베이킹함.
    /// </summary>
    public sealed class SpaceshipRegistryBaker : Baker<PrefabRegistryAuthoring>
    {
        public override void Bake(PrefabRegistryAuthoring authoring)
        {
            SpaceshipRegistryAsset spaceshipRegistry = authoring.SpaceshipRegistry;

            // 에셋 내용이 바뀌면 다시 베이킹.
            DependsOn(spaceshipRegistry);

            if (spaceshipRegistry == null)
            {
                Debug.LogWarning("SpaceshipRegistry: 레지스트리 에셋이 비어 있음.", authoring);
                return;
            }

            Entity entity = GetEntity(TransformUsageFlags.None);
            DynamicBuffer<SpaceshipRegistryElement> registry = AddBuffer<SpaceshipRegistryElement>(entity);

            // 가장 큰 Id 까지 빈 칸으로 채워 둠. 칸 번호가 곧 Id.
            int maxId = 0;

            foreach (SpaceshipAsset spaceship in spaceshipRegistry.Spaceships)
            {
                DependsOn(spaceship);

                if (spaceship != null)
                {
                    maxId = Math.Max(maxId, (int)spaceship.Id);
                }
            }

            registry.Resize(maxId + 1, NativeArrayOptions.ClearMemory);

            // 상점 UI 와 구매 검증은 에셋을 직접 읽음.
            AddComponentObject(entity, new SpaceshipRegistryAssetComponent { Value = spaceshipRegistry });

            foreach (SpaceshipAsset spaceship in spaceshipRegistry.Spaceships)
            {
                // 프리팹이 없는 기종은 상점에 보이기만 함.
                if (spaceship == null || spaceship.Id == SpaceshipId.None || spaceship.Prefab == null)
                {
                    continue;
                }

                if (registry[(int)spaceship.Id].Prefab != Entity.Null)
                {
                    Debug.LogWarning($"SpaceshipRegistry: Id 중복. 뒤의 항목을 버림. ({spaceship.Id})", spaceshipRegistry);
                    continue;
                }

                registry[(int)spaceship.Id] = new SpaceshipRegistryElement
                {
                    Prefab   = GetEntity(spaceship.Prefab, TransformUsageFlags.Dynamic),
                    Category = spaceship.Category,
                };
            }
        }
    }
}
