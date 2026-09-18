using GalaxyIO.DOTS.Authoring.Config;
using Unity.Entities;
using Unity.Mathematics;

namespace GalaxyIO.DOTS.Components.Singleton
{
    /// <summary>
    /// <see cref="EnemySpawnConfigAuthoring"/>의 DOTS 구조체. 월드당 한 개.
    /// </summary>
    public struct EnemySpawnConfigComponent : IComponentData
    {
        // 적 총 수 상한. 넘으면 스폰하지 않음.
        public int Cap;

        // 스폰 주기(초)
        public float SpawnInterval;

        // 스폰 주기마다 레이더 하나에 스폰하는 적 수
        public int SpawnPerRadar;

        // 레이더 하나 안의 적 수 상한
        public int RadarCap;

        // 플레이어 기준 스폰 거리 (최소, 최대)
        public float2 SpawnDistanceRange;

        // 플레이어 시야 사각형의 반크기. 이 안에는 스폰하지 않음.
        public float2 ViewHalfExtents;
    }
}
