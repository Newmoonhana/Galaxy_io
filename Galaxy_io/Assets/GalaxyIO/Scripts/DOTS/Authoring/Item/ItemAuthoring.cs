using GalaxyIO.DOTS.Components.Actor;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

namespace GalaxyIO.DOTS.Authoring
{
    /// <summary>
    /// 월드에 떨어진 아이템 Entity. 모든 아이템이 이 프리팹 하나를 공유하고,
    /// 스폰 시 서버가 아이템 종류와 줍기 충돌체 크기를 채움.
    /// </summary>
    [RequireComponent(typeof(SpriteRenderer))]
    public sealed class ItemAuthoring : MonoBehaviour
    {
        [SerializeField]
        [Tooltip("플레이어를 감지하는 원의 반지름")]
        [Min(0.0F)]
        public float AttractionRadius = 6.0F;

        [SerializeField]
        [Tooltip("감지한 플레이어에게 끌려가는 속력. 플레이어 최고 속력보다 커야 따라잡음.")]
        [Min(0.0F)]
        public float FollowSpeed = 40.0F;

        [SerializeField]
        [Tooltip("스폰 직후 무작위 방향으로 미끄러지는 최대 속력")]
        [Min(0.0F)]
        public float SlideSpeed = 14.0F;

        [SerializeField]
        [Tooltip("최대 속력으로 미끄러질 때 떠다니는 속력까지 느려지는 시간(초)")]
        [Min(0.01F)]
        public float SlideDuration = 0.6F;

        [SerializeField]
        [Tooltip("미끄러짐이 끝난 뒤 계속 떠다니는 속력. 플레이어 우주선의 HoveringSpeed 보다 조금 느리게.")]
        [Min(0.0F)]
        public float FloatSpeed = 7.0F;

        [SerializeField]
        [Tooltip("떠다니며 도는 각속도(도/초). 오른쪽으로 가면 시계 방향, 왼쪽이면 반시계 방향.")]
        [Min(0.0F)]
        public float SpinSpeed = 30.0F;

        [SerializeField]
        [Tooltip("아무도 줍지 않으면 사라지기까지의 시간(초)")]
        [Min(0.0F)]
        public float Lifetime = 60.0F;

        [SerializeField]
        [Tooltip("우주선이 파괴되며 떨어뜨린 아이템을 주울 수 없는 시간(초). 튕기는 모습을 보여주기 위함.")]
        [Min(0.0F)]
        public float DropPickupDelay = 0.5F;

        [SerializeField]
        [Tooltip("플레이어가 버린 아이템을 기수 방향으로 던지는 속력")]
        [Min(0.0F)]
        public float ThrowSpeed = 15.0F;

        [SerializeField]
        [Tooltip("플레이어가 버린 아이템을 주울 수 없는 시간(초). 버리자마자 다시 줍는 것 방지.")]
        [Min(0.0F)]
        public float ThrowPickupDelay = 1.5F;

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(1.0F, 0.9F, 0.3F, 0.8F);
            Gizmos.DrawWireSphere(transform.position, AttractionRadius * transform.lossyScale.x);
        }
    }

    public sealed class ItemBaker : Baker<ItemAuthoring>
    {
        public override void Bake(ItemAuthoring authoring)
        {
            Entity entity = GetEntity(TransformUsageFlags.Dynamic);

            AddComponent(entity, new ItemComponent
            {
                Target            = Entity.Null,
                Attraction        = new ColliderComponent
                {
                    Shape   = ColliderShape.Circle,
                    Center  = float2.zero,
                    Extents = new float2(authoring.AttractionRadius, 0.0F),
                },
                FollowSpeed       = authoring.FollowSpeed,
                Velocity          = float2.zero,
                SlideSpeed        = authoring.SlideSpeed,
                SlideDeceleration = math.max(authoring.SlideSpeed - authoring.FloatSpeed, 0.0F) / authoring.SlideDuration,
                FloatSpeed        = authoring.FloatSpeed,
                SpinSpeed         = math.radians(authoring.SpinSpeed),
                Lifetime          = authoring.Lifetime,
                DropPickupDelay   = authoring.DropPickupDelay,
                ThrowSpeed        = authoring.ThrowSpeed,
                ThrowPickupDelay  = authoring.ThrowPickupDelay,
            });

            // 크기는 스폰 시 아이템 스프라이트에 맞춤.
            AddComponent(entity, new ColliderComponent { Shape = ColliderShape.Obb });

            AddBuffer<ItemOverlapElement>(entity);
        }
    }
}
