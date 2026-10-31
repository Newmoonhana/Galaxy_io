using GalaxyIO.DOTS.Components.Actor;
using Unity.Burst;
using Unity.Entities;
using Unity.NetCode;

namespace GalaxyIO.DOTS.Systems
{
    /// <summary>
    /// 강화 단계(<see cref="UpgradeLevelElement"/>)를 우주선 능력치에 반영하는 시스템.
    /// 단계만 복제하고 능력치는 서버와 클라이언트가 각자 같은 식으로 계산함. 최대 체력은 서버만 계산해 복제함.
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
            state.RequireForUpdate<UpgradeParamElement>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            bool isServer = state.WorldUnmanaged.IsServer();

            foreach (
                var (spec, movement, heat, parameters, levels) in
                SystemAPI.Query<
                    RefRW<SpaceshipSpecComponent>,
                    RefRW<PlayerSpaceshipMovementComponent>,
                    RefRW<TurretHeatComponent>,
                    DynamicBuffer<UpgradeParamElement>,
                    DynamicBuffer<UpgradeLevelElement>
                >()
            ) {
                if (isServer)
                {
                    spec.ValueRW.MaxHealth = Upgrade.Value(parameters, levels, UpgradeParam.MaxHealth);
                }

                spec.ValueRW.Repair = Upgrade.Value(parameters, levels, UpgradeParam.Repair);
                heat.ValueRW.MaxHeat = Upgrade.Value(parameters, levels, UpgradeParam.MaxHeat);

                movement.ValueRW.MaxSpeed = Upgrade.Value(parameters, levels, UpgradeParam.MaxSpeed);
                movement.ValueRW.Acceleration = Upgrade.Value(parameters, levels, UpgradeParam.Acceleration);
                movement.ValueRW.Deceleration = Upgrade.Value(parameters, levels, UpgradeParam.Deceleration);
                movement.ValueRW.HoveringSpeed = Upgrade.Value(parameters, levels, UpgradeParam.HoveringSpeed);
                movement.ValueRW.HoveringAcceleration = Upgrade.Value(parameters, levels, UpgradeParam.HoveringAcceleration);
                movement.ValueRW.HoveringDeceleration = Upgrade.Value(parameters, levels, UpgradeParam.HoveringDeceleration);
                movement.ValueRW.Maneuver = Upgrade.Value(parameters, levels, UpgradeParam.Maneuver);
                movement.ValueRW.ManeuverPenalty = Upgrade.Value(parameters, levels, UpgradeParam.ManeuverPenalty);
                movement.ValueRW.HoveringManeuver = Upgrade.Value(parameters, levels, UpgradeParam.HoveringManeuver);
            }
        }
    }
}
