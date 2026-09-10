using GalaxyIO.DOTS.Collision;
using Unity.Entities;

namespace GalaxyIO.DOTS.Components.Collision
{
    /// <summary>
    /// 우주선 충돌체의 틱별 기록. 지연이 있는 클라이언트의 명중 판정을 그 시점으로 되감는 데 씀.
    /// 서버만 채움.
    /// </summary>
    [InternalBufferCapacity(0)]
    public struct SpaceshipColliderHistory : IBufferElementData
    {
        // 보관 틱 수. 60Hz에서 약 0.53초.
        public const int Length = 32;

        // 이 칸을 채운 틱. 아직 안 채웠거나 한 바퀴 돌아 낡은 칸을 걸러내는 데 씀.
        public uint Tick;

        public ColliderWorld Collider;
    }
}