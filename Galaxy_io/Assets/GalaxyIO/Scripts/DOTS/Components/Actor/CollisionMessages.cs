using Unity.Entities;
using Unity.Mathematics;

namespace GalaxyIO.DOTS.Components.Actor
{
    /// <summary>
    /// 한 틱 동안 성립한 겹침 하나. 판정 결과만 담고 행동은 소비 시스템이 정함.
    /// </summary>
    public struct CollisionMessage
    {
        // 겹친 상대 Entity
        public Entity Other;

        // 상대 충돌체의 월드 중심
        public float2 OtherCenter;

        // 상대에게서 떨어지는 방향(단위 벡터)
        public float2 Normal;

        // 겹친 깊이
        public float Depth;
    }

    /// <summary>
    /// 선체 - 선체 충돌. 우주선마다 자기 몫이 쌓임.
    /// </summary>
    [InternalBufferCapacity(0)]
    public struct SpaceshipCollisionElement : IBufferElementData
    {
        public CollisionMessage Message;
    }

    /// <summary>
    /// 투사체 - 선체 충돌. 투사체 쪽에만, 가장 먼저 맞은 하나만 쌓임.
    /// </summary>
    [InternalBufferCapacity(0)]
    public struct ProjectileHitElement : IBufferElementData
    {
        public CollisionMessage Message;
    }

    /// <summary>
    /// 시야 - 선체 충돌. 시야를 가진 AI 쪽에만 가까운 순으로 쌓임.
    /// </summary>
    [InternalBufferCapacity(0)]
    public struct SightOverlapElement : IBufferElementData
    {
        public CollisionMessage Message;
    }
}
