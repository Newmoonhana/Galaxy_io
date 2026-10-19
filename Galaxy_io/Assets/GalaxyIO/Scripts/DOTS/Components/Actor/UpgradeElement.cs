using GalaxyIO.DOTS.Authoring;
using Unity.Entities;
using Unity.NetCode;

namespace GalaxyIO.DOTS.Components.Actor
{
    /// <summary>
    /// 우주선 강화 종류. 값이 곧 <see cref="UpgradeElement"/>, <see cref="UpgradeLevelElement"/>의 인덱스.
    /// 강화 창은 최대 단계가 0 인(지원하지 않는) 종류를 빼고 이 순서로 보여줌.
    /// </summary>
    public enum UpgradeStat : byte
    {
        Damage = 0,
        MaxHeat = 1,

        // 함재기 강화
        Reinforcement = 2,

        // 생산성 증대
        Assembly = 3,

        MaxHealth = 4,
        Repair = 5,
        Speed = 6,
        Maneuver = 7,
        RadarRange = 8,
        RadarSweep = 9,
    }

    /// <summary>
    /// 강화로 바뀌는 능력치. 강화 종류 하나가 여러 개를 바꿀 수 있음.
    /// 값이 곧 <see cref="UpgradeParamElement"/>의 인덱스.
    /// </summary>
    public enum UpgradeParam : byte
    {
        // 포탑 피해 배율
        Damage = 0,

        // 포탑 최대 열
        MaxHeat = 1,

        // 최대 체력
        MaxHealth = 2,

        // 자가 수리 속도
        Repair = 3,

        // 비행 최대 이동속도, 가속도, 감속도
        MaxSpeed = 4,
        Acceleration = 5,
        Deceleration = 6,

        // 호버링 최대 이동속도, 가속도, 감속도
        HoveringSpeed = 7,
        HoveringAcceleration = 8,
        HoveringDeceleration = 9,

        // 회전 속도(도/초)
        Maneuver = 10,

        // 고속 선회 억제 비율
        ManeuverPenalty = 11,

        // 호버링 방향 전환 가속도
        HoveringManeuver = 12,

        // 레이더 탐지 거리에 더하는 값
        RadarRange = 13,

        // 레이더 스캐너 회전 속도(도/초)에 더하는 값
        RadarSweep = 14,

        // 레이더 점이 나타나기까지의 시간(초)에서 빼는 값
        RadarResponse = 15,

        // 함재기 투사체 피해, 최대 체력에 더하는 값
        CraftDamage = 16,
        CraftMaxHealth = 17,

        // 함재기 생산 시간(초)에서 빼는 값
        AssemblyTime = 18,
    }

    /// <summary>
    /// <see cref="SpaceshipUpgradeAsset"/>의 DOTS 구조체. 강화 종류 하나의 한계.
    /// 현재 단계는 같은 번호의 <see cref="UpgradeLevelElement"/>에 있음.
    /// </summary>
    [InternalBufferCapacity(Upgrade.Count)]
    public struct UpgradeElement : IBufferElementData
    {
        public byte MaxLevel;
    }

    /// <summary>
    /// 강화 종류별 현재 단계. <see cref="UpgradeElement"/>와 같은 순서. 서버가 올림.
    /// </summary>
    [InternalBufferCapacity(Upgrade.Count)]
    public struct UpgradeLevelElement : IBufferElementData
    {
        [GhostField]
        public byte Value;
    }

    /// <summary>
    /// <see cref="SpaceshipUpgradeAsset"/>의 DOTS 구조체. 능력치 하나가 강화로 바뀌는 양.
    /// </summary>
    [InternalBufferCapacity(Upgrade.ParamCount)]
    public struct UpgradeParamElement : IBufferElementData
    {
        // 이 능력치를 올리는 강화 종류
        public UpgradeStat Stat;

        // 강화하지 않았을 때의 값
        public float Base;

        // 한 단계마다 더해지는 값
        public float Amount;
    }

    /// <summary>
    /// 강화 규칙. 서버 검증과 클라이언트 표시가 같이 씀.
    /// </summary>
    public static class Upgrade
    {
        // UpgradeStat 종류 수
        public const int Count = 10;

        // UpgradeParam 종류 수
        public const int ParamCount = 19;

        /// <summary>
        /// 현재 단계까지 강화한 능력치 값.
        /// </summary>
        public static float Value(
            DynamicBuffer<UpgradeParamElement> parameters,
            DynamicBuffer<UpgradeLevelElement> levels,
            UpgradeParam param)
        {
            UpgradeParamElement parameter = parameters[(int)param];

            return parameter.Base + parameter.Amount * levels[(int)parameter.Stat].Value;
        }

        /// <summary>
        /// 다음 단계로 올리는 데 드는 파워 셀 수. 올릴 때마다 1개씩 늘어남.
        /// </summary>
        public static int Cost(int level)
        {
            return level + 1;
        }
    }
}
