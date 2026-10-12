using GalaxyIO.DOTS.Authoring;
using Unity.Entities;
using Unity.NetCode;

namespace GalaxyIO.DOTS.Components.Actor
{
    /// <summary>
    /// 강화할 수 있는 우주선 능력치. 값이 곧 강화 버퍼의 인덱스.
    /// </summary>
    public enum UpgradeStat : byte
    {
        // 포탑 피해 배율
        Damage = 0,

        // 포탑 최대 열
        MaxHeat = 1,

        // 최대 체력
        MaxHealth = 2,

        // 자가 수리 속도
        Repair = 3,

        // 비행 최대 이동속도
        Speed = 4,

        // 회전 속도(도/초)
        Maneuver = 5,

        // 레이더 탐지 거리
        RadarRange = 6,

        // 레이더 스캐너 회전 속도 배율
        RadarSweep = 7,
    }

    /// <summary>
    /// <see cref="SpaceshipUpgradeAsset"/>의 DOTS 구조체. 능력치 하나의 강화 사양.
    /// 현재 단계는 같은 번호의 <see cref="UpgradeLevelElement"/>에 있음.
    /// </summary>
    [InternalBufferCapacity(Upgrade.Count)]
    public struct UpgradeElement : IBufferElementData
    {
        // 강화하지 않았을 때의 값
        public float Base;

        // 한 단계마다 더해지는 값
        public float Amount;

        public byte MaxLevel;
    }

    /// <summary>
    /// 능력치별 현재 강화 단계. <see cref="UpgradeElement"/>와 같은 순서. 서버가 올림.
    /// </summary>
    [InternalBufferCapacity(Upgrade.Count)]
    public struct UpgradeLevelElement : IBufferElementData
    {
        [GhostField]
        public byte Value;
    }

    /// <summary>
    /// 강화 규칙. 서버 검증과 클라이언트 표시가 같이 씀.
    /// </summary>
    public static class Upgrade
    {
        // UpgradeStat 종류 수
        public const int Count = 8;

        /// <summary>
        /// 현재 단계까지 강화한 능력치 값.
        /// </summary>
        public static float Value(
            DynamicBuffer<UpgradeElement> upgrades,
            DynamicBuffer<UpgradeLevelElement> levels,
            UpgradeStat stat)
        {
            return upgrades[(int)stat].Base + upgrades[(int)stat].Amount * levels[(int)stat].Value;
        }

        /// <summary>
        /// 다음 단계로 올리는 데 드는 강화 모듈 수. 올릴 때마다 1개씩 늘어남.
        /// </summary>
        public static int Cost(int level)
        {
            return level + 1;
        }
    }
}
