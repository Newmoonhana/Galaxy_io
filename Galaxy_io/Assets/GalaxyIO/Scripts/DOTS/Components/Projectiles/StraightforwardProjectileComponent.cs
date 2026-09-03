using Unity.Entities;

namespace GalaxyIO.DOTS.Components.Projectiles
{
    /// <summary>
    /// 직선으로 나아가는 투사체
    /// </summary>
    public struct StraightforwardProjectileComponent : IComponentData
    {
        // 가속도. 음수도 가능 (속도가 점점 줄어듬)
        public float acceleration;

        // 최초 발사시 투사체 속도
        public float speed;
    }
}