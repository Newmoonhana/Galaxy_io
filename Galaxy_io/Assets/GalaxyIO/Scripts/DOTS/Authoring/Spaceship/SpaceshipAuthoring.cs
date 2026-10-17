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
    /// 우주선의 속성과 터렛 배치 정의.
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

        [Header("Attributes")]

        // 월드 좌표 상 이동속도
        public float Speed;

        // 호버링(WASD) 최대 이동속도
        [Min(0.0F)]
        public float HoveringSpeed = 10.0F;

        // 호버링 가속도. 작을수록 옆으로 미끄러지듯 붙음.
        [Min(0.0F)]
        public float HoveringAcceleration = 40.0F;

        // 호버링 감속도. WASD를 뗐을 때 초당 줄어드는 속도.
        [Min(0.0F)]
        public float HoveringDeceleration = 60.0F;

        // 호버링 방향 전환 가속도. 움직이던 중에 WASD 방향을 바꾸면 가감속 대신 이 값으로 붙음.
        [Min(0.0F)]
        public float HoveringManeuver = 40.0F;

        // 가속도
        public float Acceleration;

        // 감속도. 가속 키를 뗐을 때 초당 줄어드는 속력.
        [Min(0.0F)]
        public float Deceleration = 5.0F;

        // 회전 속도(Degree/초)
        public float Maneuver;

        // 회전 가속도(도/초²). 회전 속도가 Maneuver 까지 붙는 빠르기. 클수록 즉각적.
        [Min(1.0F)]
        public float ManeuverAcceleration = 3600.0F;

        // 회전 감속도(도/초²). 목표 방향에 가까워지면 이 값으로 회전을 멈춤. 작을수록 묵직하게 멈춤.
        [Min(1.0F)]
        public float ManeuverDeceleration = 3600.0F;

        // 고속 선회 억제: 최대 속력일 때 회전 속도가 깎이는 비율.
        [Range(0.0F, 1.0F)]
        public float ManeuverPenalty = 0.6F;

        // 최대 체력
        public float MaxHealth;

        // 자가 수리 속도
        public float Repair;

        // 피격 후 자가 수리가 다시 시작되기까지의 시간(초)
        [Min(0.0F)]
        public float RepairDelay = 3.0F;

        // 선체 두께. 보이는 선체 크기(지름)에 맞출 것.
        // 관통 투사체가 이 우주선을 지날 때 관통력이 이만큼 줄어들고, AI 는 이 절반만큼 더 떨어져서 싸움.
        [Min(0.0F)]
        public float Thickness = 1.0F;

        // 포탑 최대 열. 플레이어만. 0 이면 과열하지 않음.
        [Min(0.0F)]
        public float MaxHeat = 20.0F;

        [Header("Turn Brake")]

        // 기준 회전속도(도/초). 기준 속력에서 이 속도로 돌 때 TurnBrakeDeceleration 만큼 감속함. 플레이어만.
        [Min(1.0F)]
        public float TurnBrakeTurnRate = 270.0F;

        // 기준 속력에서 기준 회전속도로 돌 때 초당 줄어드는 속력
        [Min(0.0F)]
        public float TurnBrakeDeceleration = 30.0F;

        // 곡선의 휘는 정도. 1이면 직선, 클수록 느린 선회는 덜 깎이고 빠른 선회에서 급격히 커짐.
        [Min(1.0F)]
        public float TurnBrakeExponent = 3.0F;

        // 제동이 걸리기 시작하는 비행 속력. 이 이하에서는 감속하지 않음.
        [Min(0.0F)]
        public float TurnBrakeStartSpeed = 15.0F;

        // 기준 속력. StartSpeed 부터 선형으로 커져 이 속력에서 Deceleration 그대로 걸리고, 그 위로도 계속 커짐.
        [Min(0.0F)]
        public float TurnBrakeReferenceSpeed = 30.0F;

        // 회전 관성. 선회 제동이 셀수록 속도가 기수를 덜 따라 돌아 돌기 전 방향으로 밀려남.
        // 0이면 밀리지 않고, 1이면 기준 제동에서 기수를 전혀 따라 돌지 않음. 무거운 기체일수록 크게.
        [Min(0.0F)]
        public float Inertia = 1.0F;

        // 기수와 어긋난 옆 방향 속도가 초당 줄어드는 양
        [Min(0.0F)]
        public float InertiaDeceleration = 20.0F;

        // 뒤처진 속도 방향이 기수 방향을 따라 도는 속도(도/초)
        [Min(0.0F)]
        public float InertiaTurnRate = 180.0F;

        [Header("Camera")]

        // 가장 멀리 축소했을 때의 시야 크기(Orthographic Size). 플레이어만.
        [Min(1.0F)]
        public float MaxOrthographicSize = 15.0F;

        [Header("Radar")]

        // 레이더 탐지 거리. Ghost 복제 반경으로도 씀. 화면 대각선 절반보다 크게 잡을 것.
        [Min(1.0F)]
        public float RadarRange = 64.0F;

        // 레이더 스캐너 회전 속도(도/초)
        [Min(0.0F)]
        public float RadarSweepSpeed = 120.0F;

        // 스캐너가 지나간 뒤 레이더 점이 나타나기까지의 시간(초)
        [Min(0.0F)]
        public float RadarResponseTime = 0.1F;

        [Header("Faction")]

        // 소속 진영
        public FactionID Faction = FactionID.None;

        [Header("Turrets")]

        // 플레이어가 쏘는 조종 포탑의 총구 위치. 비우면 조종 포탑 칸이 없음.
        public List<Transform> Muzzles;

        // 자동 포탑 칸. 인벤토리 포탑 칸 순서(조종 포탑 칸 다음)와 같음.
        public List<AutoTurretMount> AutoTurrets;

        // 장착한 채로 시작하는 포탑. 아이템 레지스트리에 등록된 포탑이어야 함.
        public TurretItemAsset Turret;

        [Header("Upgrade")]

        // 강화 사양. 비우면 강화할 수 없는 기체.
        public SpaceshipUpgradeAsset Upgrade;
    }

    public sealed class SpaceshipBaker : Baker<SpaceshipAuthoring>
    {
        public override void Bake(SpaceshipAuthoring authoring)
        {
            Entity entity = GetEntity(TransformUsageFlags.Dynamic);

            AddComponent(entity, new SpaceshipSpecComponent
            {
                MaxHealth   = authoring.MaxHealth,
                Repair      = authoring.Repair,
                RepairDelay = authoring.RepairDelay,
                Thickness   = authoring.Thickness,
                Health      = authoring.MaxHealth,
            });

            AddComponent(entity, new SpaceshipMovementComponent
            {
                DeltaMovement = float2.zero
            });

            AddComponent(entity, new FactionComponent
            {
                Value = authoring.Faction,
            });

            AddComponent(entity, new SpaceshipKindComponent
            {
                Value = authoring.Kind,
            });

            AddComponent(entity, new SpaceshipRadarComponent
            {
                Range        = authoring.RadarRange,
                SweepSpeed   = authoring.RadarSweepSpeed,
                ResponseTime = authoring.RadarResponseTime,
            });

            var buffer = AddBuffer<TurretElement>(entity);
            var cooldowns = AddBuffer<TurretCooldownElement>(entity);

            // 서버 되감기용 충돌체 기록. 클라이언트에서는 비어 있음.
            AddBuffer<SpaceshipColliderHistory>(entity);

            AddBuffer<SpaceshipCollisionElement>(entity);

            AddComponent<CrashSoundCooldownComponent>(entity);

            DependsOn(authoring.Turret);

            ItemId turret = authoring.Turret != null ? authoring.Turret.Id : ItemId.None;

            if (authoring.Turret != null && turret == ItemId.None)
            {
                Debug.LogWarning($"Spaceship: 포탑 Id 가 None 이라 장착할 수 없음. ({authoring.Turret})", authoring);
            }

            AddComponent(entity, new EquippedTurretComponent { Value = turret });

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

            DependsOn(authoring.Upgrade);

            if (authoring.Upgrade == null)
            {
                return;
            }

            SpaceshipUpgradeAsset upgrade = authoring.Upgrade;

            // UpgradeStat 순서
            var upgrades = AddBuffer<UpgradeElement>(entity);
            upgrades.Add(new UpgradeElement { MaxLevel = (byte)upgrade.Damage.MaxLevel });
            upgrades.Add(new UpgradeElement { MaxLevel = (byte)upgrade.MaxHeat.MaxLevel });
            upgrades.Add(new UpgradeElement { MaxLevel = (byte)upgrade.MaxHealth.MaxLevel });
            upgrades.Add(new UpgradeElement { MaxLevel = (byte)upgrade.Repair.MaxLevel });
            upgrades.Add(new UpgradeElement { MaxLevel = (byte)upgrade.Speed.MaxLevel });
            upgrades.Add(new UpgradeElement { MaxLevel = (byte)upgrade.Maneuver.MaxLevel });
            upgrades.Add(new UpgradeElement { MaxLevel = (byte)upgrade.RadarRange.MaxLevel });
            upgrades.Add(new UpgradeElement { MaxLevel = (byte)upgrade.RadarSweep.MaxLevel });

            AddBuffer<UpgradeLevelElement>(entity).Resize(Upgrade.Count, NativeArrayOptions.ClearMemory);

            // UpgradeParam 순서. 배율은 1, 레이더에 더하는 값은 0 에서 시작함.
            var parameters = AddBuffer<UpgradeParamElement>(entity);
            Add(parameters, UpgradeStat.Damage, 1.0F, upgrade.Damage.Damage);
            Add(parameters, UpgradeStat.MaxHeat, authoring.MaxHeat, upgrade.MaxHeat.MaxHeat);
            Add(parameters, UpgradeStat.MaxHealth, authoring.MaxHealth, upgrade.MaxHealth.MaxHealth);
            Add(parameters, UpgradeStat.Repair, authoring.Repair, upgrade.Repair.Repair);
            Add(parameters, UpgradeStat.Speed, authoring.Speed, upgrade.Speed.MaxSpeed);
            Add(parameters, UpgradeStat.Speed, authoring.Acceleration, upgrade.Speed.Acceleration);
            Add(parameters, UpgradeStat.Speed, authoring.Deceleration, upgrade.Speed.Deceleration);
            Add(parameters, UpgradeStat.Speed, authoring.HoveringSpeed, upgrade.Speed.HoveringSpeed);
            Add(parameters, UpgradeStat.Speed, authoring.HoveringAcceleration, upgrade.Speed.HoveringAcceleration);
            Add(parameters, UpgradeStat.Speed, authoring.HoveringDeceleration, upgrade.Speed.HoveringDeceleration);
            Add(parameters, UpgradeStat.Maneuver, authoring.Maneuver, upgrade.Maneuver.Maneuver);
            Add(parameters, UpgradeStat.Maneuver, authoring.ManeuverPenalty, -upgrade.Maneuver.ManeuverPenalty);
            Add(parameters, UpgradeStat.Maneuver, authoring.HoveringManeuver, upgrade.Maneuver.HoveringManeuver);
            Add(parameters, UpgradeStat.RadarRange, 0.0F, upgrade.RadarRange.Range);
            Add(parameters, UpgradeStat.RadarSweep, 0.0F, upgrade.RadarSweep.SweepSpeed);
            Add(parameters, UpgradeStat.RadarSweep, 0.0F, upgrade.RadarSweep.ResponseTime);
        }

        private static void Add(DynamicBuffer<UpgradeParamElement> parameters, UpgradeStat stat, float baseValue, float amount)
        {
            parameters.Add(new UpgradeParamElement { Stat = stat, Base = baseValue, Amount = amount });
        }
    }
}