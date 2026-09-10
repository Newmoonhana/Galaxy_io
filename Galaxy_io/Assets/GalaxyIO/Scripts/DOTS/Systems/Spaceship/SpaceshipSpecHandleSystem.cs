using GalaxyIO.DOTS.Components;
using Unity.Burst;
using Unity.Entities;
using Unity.Mathematics;

namespace GalaxyIO.DOTS.Systems
{
    /// <summary>
    /// 우주선의 체력, 자가 수리 등 <see cref="SpaceshipSpecComponent"/>를 다루는 시스템.
    /// </summary>
    [BurstCompile]
    [WorldSystemFilter(WorldSystemFilterFlags.ServerSimulation)]
    [UpdateInGroup(typeof(GalaxyIOSpawnGroup))]
    public partial struct SpaceshipSpecHandleSystem : ISystem
    {
        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate(SystemAPI.QueryBuilder().WithAll<SpaceshipSpecComponent>().Build());
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            state.Dependency = new SpaceshipRepairJob
            {
                DeltaTime = SystemAPI.Time.DeltaTime,
            }.ScheduleParallel(state.Dependency);
        }
    }

    /// <summary>
    /// 시간에 비례해 체력을 회복시킴.
    /// </summary>
    [BurstCompile]
    [WithAll(typeof(Simulate))]
    public partial struct SpaceshipRepairJob : IJobEntity
    {
        public float DeltaTime;

        private void Execute(ref SpaceshipSpecComponent spec)
        {
            // 피격 직후에는 수리하지 않음.
            if (spec.RepairCooldown > 0.0F)
            {
                spec.RepairCooldown -= DeltaTime;
                return;
            }

            spec.Health = math.min(spec.Health + spec.Repair * DeltaTime, spec.MaxHealth);
        }
    }
}