using GalaxyIO.DOTS.Authoring;
using Unity.Entities;
using Unity.Transforms;

namespace GalaxyIO.DOTS.Components
{
    /// <summary>
    /// <see cref="TurretAuthoring"/>의 DOTS 구조체.
    /// </summary>
    public struct TurretElement : IBufferElementData
    {
        // 발사체 발사 위치 (로컬)
        public LocalTransform MuzzleLocation;

        // 최대 발사 쿨다운
        public float MaxFireCooldown;

        // 현재 발사 쿨다운
        public float FireCooldown;

        // 탄 퍼짐 정도(Radian)
        public float Spread;

        // 터렛에서 발사되는 투사체
        public Entity ProjectilePrefab;
    }
}