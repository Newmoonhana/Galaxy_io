using GalaxyIO.DOTS.Components.Actor;
using Unity.Burst;
using Unity.Entities;
using Unity.NetCode;

namespace GalaxyIO.DOTS.Systems
{
    /// <summary>
    /// 강화 단계(<see cref="UpgradeLevelElement"/>)를 우주선 능력치에 반영하는 시스템.
    /// 단계만 복제하고 능력치는 서버와 클라이언트가 각자 같은 식으로 계산함.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(SimulationSystemGroup), OrderFirst = true)]
    [UpdateAfter(typeof(GhostSimulationSystemGroup))]
    [UpdateBefore(typeof(PredictedSimulationSystemGroup))]
    public partial struct SpaceshipUpgradeSystem : ISystem
    {
        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<UpgradeElement>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            foreach (
                var (spec, movement, heat, upgrades, levels) in
                SystemAPI.Query<
                    RefRW<SpaceshipSpecComponent>,
                    RefRW<PlayerSpaceshipMovementComponent>,
                    RefRW<TurretHeatComponent>,
                    DynamicBuffer<UpgradeElement>,
                    DynamicBuffer<UpgradeLevelElement>
                >()
            ) {
                spec.ValueRW.MaxHealth = Upgrade.Value(upgrades, levels, UpgradeStat.MaxHealth);
                spec.ValueRW.Repair = Upgrade.Value(upgrades, levels, UpgradeStat.Repair);
                movement.ValueRW.MaxSpeed = Upgrade.Value(upgrades, levels, UpgradeStat.Speed);
                movement.ValueRW.Maneuver = Upgrade.Value(upgrades, levels, UpgradeStat.Maneuver);
                heat.ValueRW.MaxHeat = Upgrade.Value(upgrades, levels, UpgradeStat.MaxHeat);
            }
        }
    }
}
