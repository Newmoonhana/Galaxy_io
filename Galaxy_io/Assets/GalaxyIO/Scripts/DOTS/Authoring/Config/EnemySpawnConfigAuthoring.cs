using GalaxyIO.DOTS.Components.Singleton;
using Unity.Entities;
using Unity.Mathematics;
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
        [Tooltip("스폰 주기마다 레이더 하나에 스폰하는 적 수")]
        [Min(0)]
        public int SpawnPerRadar = 1;

        [SerializeField]
        [Tooltip("레이더 하나 안의 적 수 상한. 넘으면 그 레이더에는 스폰하지 않음.")]
        [Min(0)]
        public int RadarCap = 8;

        [SerializeField]
        [Tooltip("플레이어 기준 스폰 거리 (최소, 최대). 최대는 RelevancyConfigAuthoring.Radius 를 넘지 않음.")]
        public Vector2 SpawnDistanceRange = new Vector2(40.0F, 64.0F);

        [SerializeField]
        [Tooltip("플레이어 시야 사각형의 반크기. 이 안에는 스폰하지 않음. 카메라 앞서감 거리를 포함할 것.")]
        public Vector2 ViewHalfExtents = new Vector2(50.0F, 30.0F);
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
                SpawnDistanceRange = new float2(
                    math.min(authoring.SpawnDistanceRange.x, authoring.SpawnDistanceRange.y),
                    math.max(authoring.SpawnDistanceRange.x, authoring.SpawnDistanceRange.y)
                ),
                ViewHalfExtents    = math.abs(new float2(authoring.ViewHalfExtents.x, authoring.ViewHalfExtents.y)),
            });
        }
    }
}
