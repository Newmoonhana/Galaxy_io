using System;
using UnityEngine;

namespace GalaxyIO.DOTS.Authoring
{
    /// <summary>
    /// 우주선 기종 하나의 강화 사양. <see cref="SpaceshipAuthoring"/>이 참조함.
    /// 값은 모두 한 단계마다 바뀌는 양.
    /// </summary>
    [CreateAssetMenu(fileName = "SO_Upgrade", menuName = "GalaxyIO/Spaceship Upgrade")]
    public sealed class SpaceshipUpgradeAsset : ScriptableObject
    {
        [Serializable]
        public sealed class DamageUpgrade
        {
            [Tooltip("포탑 피해 배율 증가 (0.1 = 10%)")]
            public float Damage = 0.1F;

            [Range(0, 255)]
            public int MaxLevel = 10;
        }

        [Serializable]
        public sealed class MaxHeatUpgrade
        {
            [Tooltip("포탑 최대 열 증가")]
            public float MaxHeat = 10.0F;

            [Range(0, 255)]
            public int MaxLevel = 10;
        }

        [Serializable]
        public sealed class MaxHealthUpgrade
        {
            [Tooltip("최대 체력 증가")]
            public float MaxHealth = 10.0F;

            [Range(0, 255)]
            public int MaxLevel = 25;
        }

        [Serializable]
        public sealed class RepairUpgrade
        {
            [Tooltip("초당 자가 수리량 증가")]
            public float Repair = 2.0F;

            [Range(0, 255)]
            public int MaxLevel = 25;
        }

        [Serializable]
        public sealed class SpeedUpgrade
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

            [Range(0, 255)]
            public int MaxLevel = 25;
        }

        [Serializable]
        public sealed class ManeuverUpgrade
        {
            [Tooltip("회전 속도(도/초) 증가")]
            public float Maneuver = 4.0F;

            [Tooltip("고속 선회 억제 비율 감소")]
            public float ManeuverPenalty = 0.1F;

            [Tooltip("호버링 방향 전환 가속도 증가")]
            public float HoveringManeuver = 0.1F;

            [Range(0, 255)]
            public int MaxLevel = 25;
        }

        [Serializable]
        public sealed class RadarRangeUpgrade
        {
            [Tooltip("레이더 탐지 거리 증가")]
            public float Range = 10.0F;

            [Range(0, 255)]
            public int MaxLevel = 25;
        }

        [Serializable]
        public sealed class RadarSweepUpgrade
        {
            [Tooltip("레이더 스캐너 회전 속도(도/초) 증가")]
            public float SweepSpeed = 10.0F;

            [Tooltip("스캐너가 지나간 뒤 점이 나타나기까지의 시간(초) 감소")]
            public float ResponseTime = 0.08F;

            [Range(0, 255)]
            public int MaxLevel = 25;
        }

        public DamageUpgrade Damage = new DamageUpgrade();
        public MaxHeatUpgrade MaxHeat = new MaxHeatUpgrade();
        public MaxHealthUpgrade MaxHealth = new MaxHealthUpgrade();
        public RepairUpgrade Repair = new RepairUpgrade();
        public SpeedUpgrade Speed = new SpeedUpgrade();
        public ManeuverUpgrade Maneuver = new ManeuverUpgrade();
        public RadarRangeUpgrade RadarRange = new RadarRangeUpgrade();
        public RadarSweepUpgrade RadarSweep = new RadarSweepUpgrade();
    }
}
