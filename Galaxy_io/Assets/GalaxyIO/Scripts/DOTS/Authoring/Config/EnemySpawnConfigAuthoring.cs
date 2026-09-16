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
        [Tooltip("스폰 주기마다 [-500, 500) 구간의 10 간격 격자점에 한 대씩 스폰. (한 번에 10000대)")]
        public bool StressTest = false;

        [SerializeField]
        [Tooltip("적 총 수 상한. 에디터에서 눈으로 볼 때는 2000 정도로 낮출 것.")]
        [Min(0)]
        public int Cap = 500000;
    }

    public sealed class EnemySpawnConfigBaker : Baker<EnemySpawnConfigAuthoring>
    {
        public override void Bake(EnemySpawnConfigAuthoring authoring)
        {
            Entity entity = GetEntity(TransformUsageFlags.None);

            AddComponent(entity, new EnemySpawnConfigComponent
            {
                StressTest = authoring.StressTest,
                Cap        = authoring.Cap,
            });
        }
    }
}
