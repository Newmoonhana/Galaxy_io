using GalaxyIO.DOTS.Components.Actor;
using GalaxyIO.DOTS.Components.Singleton;
using System;
using System.Collections.Generic;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;

namespace GalaxyIO.DOTS.Authoring
{
    /// <summary>
    /// 우주선의 기종, 사양, 터렛 배치 정의.
    /// </summary>
    public sealed class SpaceshipAuthoring : MonoBehaviour
    {
        /// <summary>
        /// 자동 포탑 칸 하나. 포탑은 부모 기준 처음 회전에서 시야각 절반만큼 양쪽으로 돎.
        /// </summary>
        [Serializable]
        public struct AutoTurretMount
        {
            [Tooltip("돌아가는 포탑. 이 위치가 회전 중심.")]
            public Transform Turret;

            [Tooltip("포탑 그림. 빈 칸이면 감춤.")]
            public SpriteRenderer Sprite;

            [Tooltip("포탑의 자손. 포탑 회전 중심에서 이 위치로의 방향이 시야각의 가운데.")]
            public Transform Muzzle;
        }

        // 상점 기종. 상점에서 살 수 없는 기체는 None.
        public SpaceshipId Kind = SpaceshipId.None;

        // 기체 사양
        public SpaceshipSpecAsset Spec;

        [Header("Turrets")]

        // 플레이어가 쏘는 조종 포탑의 총구 위치. 비우면 조종 포탑 칸이 없음.
        public List<Transform> Muzzles;

        // 자동 포탑 칸. 인벤토리 포탑 칸 순서(조종 포탑 칸 다음)와 같음.
        public List<AutoTurretMount> AutoTurrets;

        // 장착한 채로 시작하는 포탑. 아이템 레지스트리에 등록된 포탑이어야 함.
        public TurretItemAsset Turret;

        // 조종 포탑을 바꿔 낄 수 없음. 인벤토리에 조종 포탑 칸이 없음. e.g. 순양함 함포.
        public bool FixedTurret;
    }

    public sealed class SpaceshipBaker : Baker<SpaceshipAuthoring>
    {
        public override void Bake(SpaceshipAuthoring authoring)
        {
            DependsOn(authoring.Spec);

            if (authoring.Spec == null)
            {
                Debug.LogError($"Spaceship: 기체 사양(Spec)이 비어 있음. ({authoring.name})", authoring);
                return;
            }

            SpaceshipSpecAsset spec = authoring.Spec;
            Entity entity = GetEntity(TransformUsageFlags.Dynamic);

            AddComponent(entity, new SpaceshipSpecComponent
            {
                MaxHealth   = spec.MaxHealth,
                Repair      = spec.Repair,
                RepairDelay = spec.RepairDelay,
                Thickness   = spec.Thickness,
                KillPoint   = spec.KillPoint,
                Health      = spec.MaxHealth,
            });

            AddComponent<MovementComponent>(entity);

            AddComponent(entity, new FactionComponent
            {
                Value = spec.Faction,
            });

            AddComponent(entity, new SpaceshipKindComponent
            {
                Value = authoring.Kind,
            });

            AddComponent(entity, new SpaceshipRadarComponent
            {
                Range        = spec.RadarRange,
                SweepSpeed   = spec.RadarSweepSpeed,
                ResponseTime = spec.RadarResponseTime,
            });

            var buffer = AddBuffer<TurretElement>(entity);
            var cooldowns = AddBuffer<TurretCooldownElement>(entity);

            // 서버 되감기용 충돌체 기록. 클라이언트에서는 비어 있음.
            AddBuffer<SpaceshipColliderHistory>(entity);

            AddBuffer<SpaceshipCollisionElement>(entity);

            // 데미지 숫자, 명중음 알림용. 서버만 채움.
            AddBuffer<DamageReportElement>(entity);

            AddComponent<CrashSoundCooldownComponent>(entity);

            DependsOn(authoring.Turret);

            ItemId turret = authoring.Turret != null ? authoring.Turret.Id : ItemId.None;

            if (authoring.Turret != null && turret == ItemId.None)
            {
                Debug.LogWarning($"Spaceship: 포탑 Id 가 None 이라 장착할 수 없음. ({authoring.Turret})", authoring);
            }

            AddComponent(entity, new EquippedTurretComponent { Value = turret });

            if (authoring.FixedTurret)
            {
                AddComponent<FixedTurretTag>(entity);
            }

            AddComponent<TurretStandbyComponent>(entity);

            AddComponent<TurretSwitchComponent>(entity);

            // 클라이언트가 Ghost 가 아닌 투사체를 직접 쏠 때 씀.
            AddComponent<LocalLaunchComponent>(entity);
            AddBuffer<LocalLaunchCooldownElement>(entity);

            for (int i = 0; i < authoring.Muzzles.Count; i++)
            {
                Transform t = authoring.Muzzles[i];

                buffer.Add(new TurretElement
                {
                    MuzzleLocation = LocalTransform.FromPositionRotationScale(
                        t.localPosition,
                        t.localRotation,
                        t.localScale.x
                    ),
                });

                // 첫 총구는 바로 쏠 수 있고, 뒤 총구는 첫 총구를 기다림.
                cooldowns.Add(new TurretCooldownElement { Value = i == 0 ? 0.0F : TurretCycle.Waiting });
            }

            // 자동 포탑은 빈 칸으로 시작함.
            var autoTurrets = AddBuffer<AutoTurretElement>(entity);
            var mounts = AddBuffer<AutoTurretMountElement>(entity);

            foreach (SpaceshipAuthoring.AutoTurretMount mount in authoring.AutoTurrets)
            {
                Vector3 pivot = authoring.transform.InverseTransformPoint(mount.Turret.position);
                Vector3 muzzle = authoring.transform.InverseTransformPoint(mount.Muzzle.position);

                autoTurrets.Add(default);
                mounts.Add(new AutoTurretMountElement
                {
                    Turret         = GetEntity(mount.Turret, TransformUsageFlags.Dynamic),
                    Sprite         = GetEntity(mount.Sprite, TransformUsageFlags.Dynamic),
                    Rest           = mount.Turret.localRotation,
                    Pivot          = new float2(pivot.x, pivot.y),
                    Barrel         = new float2(muzzle.x - pivot.x, muzzle.y - pivot.y),
                    SweepDirection = 1.0F,
                });
            }

            DependsOn(spec.Upgrade);

            if (spec.Upgrade == null)
            {
                return;
            }

            SpaceshipUpgradeAsset upgrade = spec.Upgrade;

            // UpgradeStat 순서. 지원하지 않는 강화는 최대 단계 0 이라 강화 창에서 빠짐.
            var upgrades = AddBuffer<UpgradeElement>(entity);

            for (int i = 0; i < Upgrade.Count; i++)
                upgrades.Add(new UpgradeElement { MaxLevel = (byte)upgrade.Get((UpgradeStat)i).SupportedMaxLevel });

            AddBuffer<UpgradeLevelElement>(entity).Resize(Upgrade.Count, NativeArrayOptions.ClearMemory);

            // UpgradeParam 순서. 배율은 1, 레이더에 더하는 값은 0 에서 시작함.
            var parameters = AddBuffer<UpgradeParamElement>(entity);
            Add(parameters, UpgradeStat.Damage, 1.0F, upgrade.Damage.Damage);
            Add(parameters, UpgradeStat.MaxHeat, spec.MaxHeat, upgrade.MaxHeat.MaxHeat);
            Add(parameters, UpgradeStat.MaxHealth, spec.MaxHealth, upgrade.MaxHealth.MaxHealth);
            Add(parameters, UpgradeStat.Repair, spec.Repair, upgrade.Repair.Repair);
            Add(parameters, UpgradeStat.Speed, spec.Speed, upgrade.Speed.MaxSpeed);
            Add(parameters, UpgradeStat.Speed, spec.Acceleration, upgrade.Speed.Acceleration);
            Add(parameters, UpgradeStat.Speed, spec.Deceleration, upgrade.Speed.Deceleration);
            Add(parameters, UpgradeStat.Speed, spec.HoveringSpeed, upgrade.Speed.HoveringSpeed);
            Add(parameters, UpgradeStat.Speed, spec.HoveringAcceleration, upgrade.Speed.HoveringAcceleration);
            Add(parameters, UpgradeStat.Speed, spec.HoveringDeceleration, upgrade.Speed.HoveringDeceleration);
            Add(parameters, UpgradeStat.Maneuver, spec.Maneuver, upgrade.Maneuver.Maneuver);
            Add(parameters, UpgradeStat.Maneuver, spec.ManeuverPenalty, -upgrade.Maneuver.ManeuverPenalty);
            Add(parameters, UpgradeStat.Maneuver, spec.HoveringManeuver, upgrade.Maneuver.HoveringManeuver);
            Add(parameters, UpgradeStat.RadarRange, 0.0F, upgrade.RadarRange.Range);
            Add(parameters, UpgradeStat.RadarSweep, 0.0F, upgrade.RadarSweep.SweepSpeed);
            Add(parameters, UpgradeStat.RadarSweep, 0.0F, upgrade.RadarSweep.ResponseTime);
            Add(parameters, UpgradeStat.Reinforcement, 0.0F, upgrade.Reinforcement.Damage);
            Add(parameters, UpgradeStat.Reinforcement, 0.0F, upgrade.Reinforcement.MaxHealth);
            Add(parameters, UpgradeStat.Assembly, 0.0F, upgrade.Assembly.AssemblyTime);
            Add(parameters, UpgradeStat.Reload, 0.0F, upgrade.Reload.ReloadTime);
        }

        private static void Add(DynamicBuffer<UpgradeParamElement> parameters, UpgradeStat stat, float baseValue, float amount)
        {
            parameters.Add(new UpgradeParamElement { Stat = stat, Base = baseValue, Amount = amount });
        }
    }
}