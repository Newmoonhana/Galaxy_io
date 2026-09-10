using GalaxyIO.DOTS.Client.Systems;
using Unity.Entities;
using Unity.NetCode;

namespace GalaxyIO.DOTS.Components.Projectiles
{
    /// <summary>
    /// 예측 스폰한 투사체와 서버가 보낸 Ghost를 짝짓는 식별자.
    /// <see cref="SharedProjectileComponent"/>와 분리됨 — 발사체의 ID는
    /// <see cref="ProjectileSpawnClassificationSystem"/>에서 참조되는데,
    /// 해당 System은 <see cref="SharedProjectileComponent"/> 의 다른 필드를
    /// 필요로 하지 않기 때문.
    /// </summary>
    public struct ProjectileSpawnIdComponent : IComponentData
    {
        // 발사 주체/시점/터렛으로 만든 값. 클라이언트와 서버가 같은 값을 냄.
        // 한 번 정해지면 변하지 않아 스냅샷 비용은 사실상 0.
        [GhostField]
        public uint Value;
    }
}