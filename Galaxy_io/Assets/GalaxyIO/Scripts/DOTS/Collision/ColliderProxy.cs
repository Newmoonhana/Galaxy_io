using GalaxyIO.DOTS.Components.Actor;
using Unity.Entities;

namespace GalaxyIO.DOTS.Collision
{
    /// <summary>
    /// 충돌체의 종류. 쌍으로 만들 조합을 고르는 데 씀.
    /// </summary>
    public enum CollisionLayer : byte
    {
        None = 0,

        // 우주선 선체
        Spaceship = 1 << 0,

        // 투사체
        Projectile = 1 << 1,

        // AI 시야
        Sight = 1 << 2,

        // 아이템 감지 원
        Item = 1 << 3,
    }

    /// <summary>
    /// 한 틱 동안 붙잡아 두는 충돌체 하나. 판정 단위는 Entity 가 아니라 충돌체라서
    /// 한 Entity 가 선체와 시야처럼 여러 개를 낼 수 있음.
    /// </summary>
    public struct ColliderProxy
    {
        // 이 충돌체를 가진 Entity
        public Entity Entity;

        public CollisionLayer Layer;

        // 월드 좌표 충돌체. 투사체는 한 틱 이동량만큼 늘린 상태임.
        public ColliderWorld Collider;

        // Collider 의 경계 상자. 투사체는 되감기 여유분까지 포함함.
        public Aabb Bounds;

        // 소속 진영
        public FactionID Faction;

        // 소속 파티. 0 이면 파티 없음.
        public uint Party;

        // 시야, 아이템이 대상으로 삼을 진영 마스크. 시야, 아이템 충돌체만 채움.
        public uint HostileFactions;

        // 이 Entity 와는 쌍을 만들지 않음. (투사체를 쏜 우주선)
        public Entity Ignore;
    }

    /// <summary>
    /// 행동이 없는 조합을 쌍에서 걸러내는 규칙.
    /// </summary>
    public static class CollisionFilter
    {
        /// <summary>
        /// 두 충돌체가 판정할 쌍인지. 자기 자신, 무시 대상, 관심 없는 층, 우호 진영을 걸러냄.
        /// </summary>
        public static bool Accepts(in ColliderProxy receiver, in ColliderProxy target, CollisionLayer targetLayers)
        {
            if (receiver.Entity == target.Entity
                || receiver.Ignore == target.Entity
                || target.Ignore == receiver.Entity)
            {
                return false;
            }

            if (((byte)target.Layer & (byte)targetLayers) == 0)
            {
                return false;
            }

            // 시야, 아이템은 마스크의 진영만 봄.
            if (receiver.Layer == CollisionLayer.Sight || receiver.Layer == CollisionLayer.Item)
            {
                return ((uint)target.Faction & receiver.HostileFactions) != 0;
            }

            // 투사체는 같은 파티를 통과함. 선체끼리는 같은 파티여도 부딪힘.
            if (receiver.Layer == CollisionLayer.Projectile && Faction.IsSameParty(receiver.Party, target.Party))
            {
                return false;
            }

            // 선체와 투사체는 동맹이 아니면 부딪힘.
            return !Faction.IsAllied(receiver.Faction, target.Faction);
        }
    }
}