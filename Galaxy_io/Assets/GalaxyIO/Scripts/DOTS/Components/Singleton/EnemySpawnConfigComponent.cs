using GalaxyIO.DOTS.Authoring.Config;
using Unity.Entities;

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

        // 스폰 영역 바깥 원 반지름 = 레이더 탐지 거리 x 이 값. 항상 1보다 큼.
        public float SpawnAreaScale;
    }
}
