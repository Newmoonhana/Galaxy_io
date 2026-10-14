using System;
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

            Entity entity = GetEntity(TransformUsageFlags.None);
            DynamicBuffer<PrefabRegistryElement> registry = AddBuffer<PrefabRegistryElement>(entity);

            // 가장 큰 Id 까지 빈 칸으로 채워 둠. 칸 번호가 곧 Id.
            int maxId = 0;

            foreach (PrefabRegistryAsset.Entry entry in authoring.Registry.Prefabs)
            {
                maxId = Math.Max(maxId, (int)entry.Id);
            }

            registry.Resize(maxId + 1, NativeArrayOptions.ClearMemory);

            foreach (PrefabRegistryAsset.Entry entry in authoring.Registry.Prefabs)
            {
                if (entry.Id == PrefabId.None || entry.Prefab == null)
                {
                    Debug.LogWarning($"PrefabRegistry: 비어 있는 항목을 건너뜀. ({entry.Id})", authoring.Registry);
                    continue;
                }

                if (registry.Contains(entry.Id))
                {
                    Debug.LogWarning($"PrefabRegistry: Id 중복. 뒤의 항목을 버림. ({entry.Id})", authoring.Registry);
                    continue;
                }

                registry[(int)entry.Id] = new PrefabRegistryElement
                {
                    Prefab = GetEntity(entry.Prefab, TransformUsageFlags.Dynamic),
                };
            }
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

            DynamicBuffer<TurretSpecElement> turretSpecs = AddBuffer<TurretSpecElement>(entity);

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
                    Sprite          = item.Sprite,
                    HalfExtents     = new float2(extents.x, extents.y),
                    TurretSpecStart = (ushort)turretSpecs.Length,
                };

                if (item is TurretItemAsset turret)
                {
                    for (int i = 0; i < turret.TurretSpecs.Count; i++)
                    {
                        TurretItemAsset.TurretSpec spec = turret.TurretSpecs[i];

                        // 앞 총구는 혼자 쏘는 총구여야 함. 잘못 지정하면 혼자 쏘는 총구로 취급.
                        int follow = spec.FollowMuzzle - 1;

                        if (follow >= 0
                            && (follow == i
                                || follow >= turret.TurretSpecs.Count
                                || turret.TurretSpecs[follow].FollowMuzzle > 0
                                || !turret.TurretSpecs[follow].Armed))
                        {
                            Debug.LogWarning($"ItemRegistry: {turret.name} 총구 {i + 1} 의 FollowMuzzle 이 잘못됨. 혼자 쏘는 총구로 취급.", turret);
                            follow = -1;
                        }

                        turretSpecs.Add(new TurretSpecElement
                        {
                            ProjectilePrefab  = spec.Armed ? GetEntity(spec.Projectile, TransformUsageFlags.Dynamic) : Entity.Null,
                            LocalLaunch       = spec.Armed && spec.Projectile.GetComponent<GhostAuthoringComponent>() == null,
                            MaxFireCooldown   = spec.FireCooldown,
                            FollowMuzzle      = (sbyte)follow,
                            FollowDelay       = spec.FollowDelay,
                            ProjectileCount   = (byte)Math.Max(spec.ProjectileCount, 1),
                            Spread            = spec.Spread,
                            SpeedSpread       = spec.SpeedSpread,
                            Damage            = spec.Damage,
                            ShotSound         = spec.ShotSound,
                        });
                    }

                    element.TurretSpecCount = (byte)turret.TurretSpecs.Count;
                    element.SwitchTime      = turret.SwitchTime;
                    element.ManeuverPenalty = turret.ManeuverPenalty;
                    element.Heat            = turret.Heat;
                    element.HeatMode        = turret.HeatMode;
                    element.LaunchSound     = turret.LaunchSound;
                    element.LoopSound       = turret.LoopSound;
                }

                registry[(int)item.Id] = element;
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
                    Prefab = GetEntity(spaceship.Prefab, TransformUsageFlags.Dynamic),
                };
            }
        }
    }
}
