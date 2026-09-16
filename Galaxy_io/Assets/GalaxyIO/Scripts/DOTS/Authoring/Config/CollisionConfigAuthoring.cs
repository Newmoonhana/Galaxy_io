using GalaxyIO.DOTS.Components.Singleton;
using Unity.Entities;
using UnityEngine;

namespace GalaxyIO.DOTS.Authoring.Config
{
    /// <summary>
    /// 충돌 판정 설정. 월드당 한 개.
    /// </summary>
    public sealed class CollisionConfigAuthoring : MonoBehaviour
    {
        [SerializeField]
        [Tooltip("끄면 모든 충돌체와 전수 비교함. 성능 비교용.")]
        public bool UseSpatialHash = true;

        [SerializeField]
        [Tooltip("격자 칸 한 변 길이. 작을수록 후보는 줄고 등록할 칸은 늘어남.")]
        [Min(0.1F)]
        public float CellSize = 8.0F;

        [SerializeField]
        [Tooltip("되감기 여유분에 쓰는 우주선 최대 속력. 기체 최고 속력보다 크게 잡을 것.")]
        [Min(0.0F)]
        public float RewindSpeed = 16.0F;

        [SerializeField]
        [Tooltip("부딪혔을 때 튕겨 나가는 정도. 0이면 파고드는 속도만 사라짐.")]
        [Range(0.0F, 1.0F)]
        public float Restitution = 1.0F;
    }

    public sealed class CollisionConfigBaker : Baker<CollisionConfigAuthoring>
    {
        public override void Bake(CollisionConfigAuthoring authoring)
        {
            Entity entity = GetEntity(TransformUsageFlags.None);

            AddComponent(entity, new CollisionConfigComponent
            {
                UseSpatialHash = authoring.UseSpatialHash,
                CellSize       = authoring.CellSize,
                RewindSpeed    = authoring.RewindSpeed,
                Restitution    = authoring.Restitution,
            });
        }
    }
}
