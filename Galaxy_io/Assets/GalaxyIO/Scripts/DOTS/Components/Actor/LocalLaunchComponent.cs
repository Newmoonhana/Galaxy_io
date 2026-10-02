using GalaxyIO.DOTS.Client.Systems;
using Unity.Entities;
using Unity.Mathematics;

namespace GalaxyIO.DOTS.Components.Actor
{
    /// <summary>
    /// 클라이언트가 직접 발사하는 투사체(Ghost 가 아닌 투사체)를 위한 우주선 쪽 상태. 클라이언트만 씀.
    /// <see cref="LocalProjectileSystem"/>이 사격 전환 상태(<see cref="TurretSwitchComponent"/>)를 보고 발사함.
    /// </summary>
    public struct LocalLaunchComponent : IComponentData
    {
        // 직전 프레임 위치. 복제되지 않는 우주선 속도를 위치 변화로 구함.
        public float2 LastPosition;

        // LastPosition 이 채워졌는지
        public bool Tracked;
    }

    /// <summary>
    /// 총구별 클라이언트 발사 쿨다운. <see cref="TurretElement"/>와 같은 순서. 클라이언트만 씀.
    /// </summary>
    [InternalBufferCapacity(0)]
    public struct LocalLaunchCooldownElement : IBufferElementData
    {
        public float Value;
    }

    /// <summary>
    /// 클라이언트가 직접 발사한 투사체. 예측 롤백 재시뮬레이션에서 빠지고 프레임마다 한 번 움직임.
    /// </summary>
    public struct LocalProjectileTag : IComponentData
    {
    }
}
