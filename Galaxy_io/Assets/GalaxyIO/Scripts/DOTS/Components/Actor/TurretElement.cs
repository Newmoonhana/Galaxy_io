using Unity.Entities;
using Unity.Transforms;

namespace GalaxyIO.DOTS.Components.Actor
{
    /// <summary>
    /// 우주선 총구 하나. 사양(발사체, 발사 간격, 탄퍼짐, 발사음)은
    /// <see cref="EquippedTurretComponent"/>의 포탑에서 총구 번호로 찾음.
    /// </summary>
    public struct TurretElement : IBufferElementData
    {
        // 발사체 발사 위치 (로컬)
        public LocalTransform MuzzleLocation;

        // 현재 발사 쿨다운
        public float FireCooldown;
    }
}