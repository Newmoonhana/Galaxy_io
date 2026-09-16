using GalaxyIO.DOTS.Authoring.Config;
using Unity.Entities;

namespace GalaxyIO.DOTS.Components.Singleton
{
    /// <summary>
    /// <see cref="EnemySpawnConfigAuthoring"/>의 DOTS 구조체. 월드당 한 개.
    /// </summary>
    public struct EnemySpawnConfigComponent : IComponentData
    {
        // 켜면 스폰 주기마다 격자 전체에 적을 스폰함. 접속한 플레이어가 없어도 스폰함.
        public bool StressTest;

        // 적 총 수 상한. 넘으면 스폰하지 않음.
        public int Cap;
    }
}
