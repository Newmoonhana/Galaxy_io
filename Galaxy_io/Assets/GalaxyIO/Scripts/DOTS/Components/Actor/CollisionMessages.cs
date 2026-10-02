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
    /// 투사체 - 선체 충돌. 투사체 쪽에만, 이동 경로상 먼저 닿은 순으로 쌓임.
    /// </summary>
    [InternalBufferCapacity(0)]
    public struct ProjectileHitElement : IBufferElementData
    {
        public CollisionMessage Message;
    }

    /// <summary>
    /// 관통 투사체가 이미 지나간 우주선. 같은 우주선을 다시 맞히지 않음.
    /// </summary>
    [InternalBufferCapacity(0)]
    public struct ProjectilePiercedElement : IBufferElementData
    {
        public Entity Spaceship;
    }

    public static class ProjectilePierced
    {
        public static bool Contains(this DynamicBuffer<ProjectilePiercedElement> pierced, Entity spaceship)
        {
            for (int i = 0; i < pierced.Length; i++)
            {
                if (pierced[i].Spaceship == spaceship)
                {
                    return true;
                }
            }

            return false;
        }
    }

    /// <summary>
    /// 시야 - 선체 충돌. 시야를 가진 AI 쪽에만 가까운 순으로 쌓임.
    /// </summary>
    [InternalBufferCapacity(0)]
    public struct SightOverlapElement : IBufferElementData
    {
        public CollisionMessage Message;
    }

    /// <summary>
    /// 아이템 감지 원 - 플레이어 선체 충돌. 아이템 쪽에만 판정 순으로 쌓임.
    /// </summary>
    [InternalBufferCapacity(0)]
    public struct ItemOverlapElement : IBufferElementData
    {
        public CollisionMessage Message;

        // 아이템 본체(OBB)까지 닿았는지. 닿으면 주움.
        public bool Contact;
    }
}
