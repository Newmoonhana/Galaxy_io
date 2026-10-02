using GalaxyIO.DOTS.Components.Singleton;
using Unity.Entities;
using Unity.NetCode;
using Unity.Transforms;
using Random = Unity.Mathematics.Random;

namespace GalaxyIO.DOTS.Components.Actor
{
    /// <summary>
    /// 우주선 총구 하나. 사양(발사체, 발사 간격, 탄퍼짐, 발사음)은
    /// <see cref="EquippedTurretComponent"/>의 포탑에서 총구 번호로 찾음.
    /// 쿨다운은 같은 번호의 <see cref="TurretCooldownElement"/>에 있음.
    /// </summary>
    public struct TurretElement : IBufferElementData
    {
        // 발사체 발사 위치 (로컬)
        public LocalTransform MuzzleLocation;
    }

    /// <summary>
    /// 총구별 현재 발사 쿨다운. <see cref="TurretElement"/>와 같은 순서.
    /// 주인에게만 복제함. 예측 롤백 시 서버 값으로 되감아 발사 틱을 서버와 맞춤.
    /// </summary>
    [GhostComponent(OwnerSendType = SendToOwnerType.SendToOwner)]
    public struct TurretCooldownElement : IBufferElementData
    {
        // 발사 틱이 어긋나지 않도록 양자화하지 않음.
        [GhostField(Quantization = 0)]
        public float Value;
    }

    /// <summary>
    /// 한 발의 탄퍼짐, 속력 분포 규칙. 플레이어, AI 발사 시스템이 같이 씀.
    /// </summary>
    public static class TurretShot
    {
        /// <summary>
        /// 총구 방향을 탄퍼짐만큼 돌리고 속력 배율을 정함. 같은 시드면 클라이언트와 서버가 같은 값을 냄.
        /// </summary>
        public static LocalTransform Aim(LocalTransform muzzle, in TurretSpecElement spec, uint seed, out float speedScale)
        {
            var random = Random.CreateFromIndex(seed & 0x7FFFFFFFu);
            float halfSpread = spec.Spread * 0.5F;

            speedScale = 1.0F + random.NextFloat(-spec.SpeedSpread, spec.SpeedSpread);

            return halfSpread > 0.0F ? muzzle.RotateZ(random.NextFloat(-halfSpread, halfSpread)) : muzzle;
        }
    }
}
