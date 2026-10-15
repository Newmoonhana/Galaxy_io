using GalaxyIO.DOTS.Components.Singleton;
using Unity.Entities;
using UnityEngine;

namespace GalaxyIO.DOTS.Authoring.Config
{
    /// <summary>
    /// 적 스폰 설정. 월드당 한 개.
    /// </summary>
    public sealed class EnemySpawnConfigAuthoring : MonoBehaviour
    {
        [SerializeField]
        [Tooltip("적 총 수 상한.")]
        [Min(0)]
        public int Cap = 500000;

        [SerializeField]
        [Tooltip("스폰 주기(초)")]
        [Min(0.1F)]
        public float SpawnInterval = 5.0F;

        [SerializeField]
        [Tooltip("스폰 주기마다 플레이어 하나의 스폰 영역에 스폰하는 적 수")]
        [Min(0)]
        public int SpawnPerRadar = 1;

        [SerializeField]
        [Tooltip("스폰 영역 바깥 원 하나 안의 적 수 상한. 넘으면 그 플레이어 주변에는 스폰하지 않음.")]
        [Min(0)]
        public int RadarCap = 8;

        [SerializeField]
        [Tooltip("스폰 영역 바깥 원 반지름 = 레이더 탐지 거리 x 이 값. 레이더 원 밖, 바깥 원 안에 스폰함. " +
                 "바깥 원은 속력에 비례해 진행 방향으로 치우치고, 최대 속력이면 레이더 원이 뒤쪽 끝에 맞닿음.")]
        [Min(1.01F)]
        public float SpawnAreaScale = 1.5F;
    }

    public sealed class EnemySpawnConfigBaker : Baker<EnemySpawnConfigAuthoring>
    {
        public override void Bake(EnemySpawnConfigAuthoring authoring)
        {
            Entity entity = GetEntity(TransformUsageFlags.None);

            AddComponent(entity, new EnemySpawnConfigComponent
            {
                Cap                = authoring.Cap,
                SpawnInterval      = authoring.SpawnInterval,
                SpawnPerRadar      = authoring.SpawnPerRadar,
                RadarCap           = authoring.RadarCap,
                SpawnAreaScale     = authoring.SpawnAreaScale,
            });
        }
    }
}
