using System;
using GalaxyIO.DOTS.Components.Actor;
using UnityEngine;

namespace GalaxyIO.DOTS.Authoring
{
    /// <summary>
    /// 우주선 기종 하나의 강화 사양. <see cref="SpaceshipSpecAsset"/>이 참조함.
    /// 값은 모두 한 단계마다 바뀌는 양.
    /// </summary>
    [CreateAssetMenu(fileName = "SO_Upgrade", menuName = "GalaxyIO/Spaceship Upgrade")]
    public sealed class SpaceshipUpgradeAsset : ScriptableObject
    {
        /// <summary>
        /// 강화 종류 하나의 공통 사양.
        /// </summary>
        [Serializable]
        public abstract class Entry
        {
            [Tooltip("이 기종이 쓰는 강화인지. 끄면 강화 창과 사양 창에서 빠짐")]
            public bool Supported = true;

            [Range(0, 255)]
            public int MaxLevel;

            protected Entry(int maxLevel)
            {
                MaxLevel = maxLevel;
            }

            // 지원하지 않으면 0
            public int SupportedMaxLevel => Supported ? MaxLevel : 0;
        }

        [Serializable]
        public sealed class DamageUpgrade : Entry
        {
            [Tooltip("포탑 피해 배율 증가 (0.1 = 10%)")]
            public float Damage = 0.1F;

            public DamageUpgrade() : base(10) { }
        }

        [Serializable]
        public sealed class MaxHeatUpgrade : Entry
        {
            [Tooltip("포탑 최대 열 증가")]
            public float MaxHeat = 10.0F;

            public MaxHeatUpgrade() : base(10) { }
        }

        [Serializable]
        public sealed class ReloadUpgrade : Entry
        {
            [Tooltip("조종 포탑 발사 간격(초) 감소. 간격이 0 이하로 줄면 매 틱 쏨")]
            public float ReloadTime = 0.1F;

            // 열 대신 발사 간격으로 쏘는 기종(순양함)만 씀
            public ReloadUpgrade() : base(10)
            {
                Supported = false;
            }
        }

        [Serializable]
        public sealed class ReinforcementUpgrade : Entry
        {
            [Tooltip("함재기 투사체 피해 증가")]
            public float Damage = 0.7F;

            [Tooltip("함재기 최대 체력 증가")]
            public float MaxHealth = 8.0F;

            public ReinforcementUpgrade() : base(10) { }
        }

        [Serializable]
        public sealed class AssemblyUpgrade : Entry
        {
            [Tooltip("함재기 생산 시간(초) 감소")]
            public float AssemblyTime = 0.25F;

            public AssemblyUpgrade() : base(20) { }
        }

        [Serializable]
        public sealed class MaxHealthUpgrade : Entry
        {
            [Tooltip("최대 체력 증가")]
            public float MaxHealth = 10.0F;

            public MaxHealthUpgrade() : base(25) { }
        }

        [Serializable]
        public sealed class RepairUpgrade : Entry
        {
            [Tooltip("초당 자가 수리량 증가")]
            public float Repair = 2.0F;

            public RepairUpgrade() : base(25) { }
        }

        [Serializable]
        public sealed class SpeedUpgrade : Entry
        {
            [Tooltip("비행 최대 이동속도 증가")]
            public float MaxSpeed = 2.0F;

            [Tooltip("비행 가속도 증가")]
            public float Acceleration = 0.2F;

            [Tooltip("비행 감속도 증가")]
            public float Deceleration = 0.15F;

            [Tooltip("호버링 최대 이동속도 증가")]
            public float HoveringSpeed = 0.1F;

            [Tooltip("호버링 가속도 증가")]
            public float HoveringAcceleration = 0.05F;

            [Tooltip("호버링 감속도 증가")]
            public float HoveringDeceleration = 0.03F;

            public SpeedUpgrade() : base(25) { }
        }

        [Serializable]
        public sealed class ManeuverUpgrade : Entry
        {
            [Tooltip("회전 속도(도/초) 증가")]
            public float Maneuver = 4.0F;

            [Tooltip("고속 선회 억제 비율 감소")]
            public float ManeuverPenalty = 0.1F;

            [Tooltip("호버링 방향 전환 가속도 증가")]
            public float HoveringManeuver = 0.1F;

            public ManeuverUpgrade() : base(25) { }
        }

        [Serializable]
        public sealed class RadarRangeUpgrade : Entry
        {
            [Tooltip("레이더 탐지 거리 증가")]
            public float Range = 10.0F;

            public RadarRangeUpgrade() : base(25) { }
        }

        [Serializable]
        public sealed class RadarSweepUpgrade : Entry
        {
            [Tooltip("레이더 스캐너 회전 속도(도/초) 증가")]
            public float SweepSpeed = 10.0F;

            [Tooltip("스캐너가 지나간 뒤 점이 나타나기까지의 시간(초) 감소")]
            public float ResponseTime = 0.08F;

            public RadarSweepUpgrade() : base(25) { }
        }

        public DamageUpgrade Damage = new DamageUpgrade();
        public MaxHeatUpgrade MaxHeat = new MaxHeatUpgrade();
        public ReloadUpgrade Reload = new ReloadUpgrade();
        public ReinforcementUpgrade Reinforcement = new ReinforcementUpgrade();
        public AssemblyUpgrade Assembly = new AssemblyUpgrade();
        public MaxHealthUpgrade MaxHealth = new MaxHealthUpgrade();
        public RepairUpgrade Repair = new RepairUpgrade();
        public SpeedUpgrade Speed = new SpeedUpgrade();
        public ManeuverUpgrade Maneuver = new ManeuverUpgrade();
        public RadarRangeUpgrade RadarRange = new RadarRangeUpgrade();
        public RadarSweepUpgrade RadarSweep = new RadarSweepUpgrade();

        /// <summary>
        /// 강화 종류 하나의 사양.
        /// </summary>
        public Entry Get(UpgradeStat stat)
        {
            return stat switch
            {
                UpgradeStat.Damage        => Damage,
                UpgradeStat.MaxHeat       => MaxHeat,
                UpgradeStat.Reload        => Reload,
                UpgradeStat.Reinforcement => Reinforcement,
                UpgradeStat.Assembly      => Assembly,
                UpgradeStat.MaxHealth     => MaxHealth,
                UpgradeStat.Repair        => Repair,
                UpgradeStat.Speed         => Speed,
                UpgradeStat.Maneuver      => Maneuver,
                UpgradeStat.RadarRange    => RadarRange,
                UpgradeStat.RadarSweep    => RadarSweep,
                _                         => throw new ArgumentOutOfRangeException(nameof(stat), stat, null),
            };
        }
    }
}
