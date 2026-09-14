using GalaxyIO.DOTS.Authoring;
using Unity.Entities;
using Unity.NetCode;

namespace GalaxyIO.DOTS.Components.Actor
{
    /// <summary>
    /// <see cref="SpaceshipAuthoring"/>의 DOTS 구조체.
    /// </summary>
    public struct SpaceshipSpecComponent : IComponentData
    {
        // 최대 체력
        public float MaxHealth;

        // 자가 수리 속도
        public float Repair;

        // 피격 후 자가 수리가 다시 시작되기까지의 시간(초)
        public float RepairDelay;

        // 남은 수리 대기 시간. 피격할 때마다 RepairDelay로 다시 채워짐.
        public float RepairCooldown;

        // 현재 체력. 예측 롤백 시 서버 값으로 되감아야 하므로 복제함.
        [GhostField]
        public float Health;
    }
}