using GalaxyIO.DOTS.Components.Actor;
using Unity.Entities;
using UnityEngine;

namespace GalaxyIO.DOTS.Authoring
{
    /// <summary>
    /// 충돌체 설정값. 구현 클래스가 셰이프별 필드를 정의함.
    /// </summary>
    public abstract class ColliderAuthoring : MonoBehaviour
    {
        [SerializeField]
        [Tooltip("오브젝트 기준 충돌체 중심 오프셋")]
        public Vector2 Center;

        /// <summary>
        /// 베이킹할 DOTS 구조체를 만듦.
        /// </summary>
        public abstract ColliderComponent ToComponent();
    }

    /// <summary>
    /// 모든 <see cref="ColliderAuthoring"/> 파생 타입을 하나의 베이커가 처리함.
    /// </summary>
    [BakeDerivedTypes]
    public sealed class ColliderBaker : Baker<ColliderAuthoring>
    {
        public override void Bake(ColliderAuthoring authoring)
        {
            Entity entity = GetEntity(TransformUsageFlags.Dynamic);
            AddComponent(entity, authoring.ToComponent());
        }
    }
}