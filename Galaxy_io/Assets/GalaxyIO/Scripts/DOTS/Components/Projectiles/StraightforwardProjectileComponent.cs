using Unity.Entities;
using Unity.NetCode;

namespace GalaxyIO.DOTS.Components.Projectiles
{
    /// <summary>
    /// 직선으로 나아가는 투사체.
    /// </summary>
    public struct StraightforwardProjectileComponent : IComponentData
    {
        // 가속도. 음수도 가능 (속도가 점점 줄어듬)
        public float Acceleration;

        // 최초 발사시 투사체 속도. 발사 시점에 우주선 속력이 더해질 수 있음.
        // 예측 롤백 시 서버 값으로 되감아야 하므로 복제함.
        [GhostField]
        public float Speed;

        // 발사한 우주선의 속력을 Speed에 더할지 여부.
        // 기수 방향으로 나가는 투사체만 켬. 측면/후방 터렛의 투사체는 꺼야 함.
        public bool ReflectOwnerVelocity;
    }
}