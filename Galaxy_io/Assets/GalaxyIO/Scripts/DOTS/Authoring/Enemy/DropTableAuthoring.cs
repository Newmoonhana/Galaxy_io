using GalaxyIO.DOTS.Components.Actor;
using GalaxyIO.DOTS.Components.Singleton;
using Unity.Entities;
using UnityEngine;

namespace GalaxyIO.DOTS.Authoring
{
    /// <summary>
    /// 파괴 시 아이템을 떨어뜨리는 적. 테이블은 적 종류로 드랍 테이블 레지스트리에서 찾음.
    /// </summary>
    public sealed class DropTableAuthoring : MonoBehaviour
    {
        [SerializeField]
        [Tooltip("드랍 테이블을 찾을 적 종류")]
        public EnemyType Enemy;
    }

    public sealed class DropTableBaker : Baker<DropTableAuthoring>
    {
        public override void Bake(DropTableAuthoring authoring)
        {
            if (authoring.Enemy == EnemyType.None)
            {
                Debug.LogWarning("DropTable: 적 종류가 비어 있음.", authoring);
                return;
            }

            AddComponent(GetEntity(TransformUsageFlags.Dynamic), new DropTableComponent { Enemy = authoring.Enemy });
        }
    }
}
