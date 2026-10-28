using GalaxyIO.DOTS.Components.Actor;
using Unity.Burst;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace GalaxyIO.DOTS.Systems
{
    /// <summary>
    /// <see cref="StraightforwardProjectileComponent"/>를 가진 투사체의 속력을 갱신하고
    /// 자신의 Up 방향으로 속도를 정하는 시스템.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(UpdateSystemGroup))]
    public partial struct StraightforwardProjectileSystem : ISystem
    {
        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate(SystemAPI.QueryBuilder().WithAll<StraightforwardProjectileComponent>().Build());
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            state.Dependency = new StraightforwardProjectileJob
            {
                DeltaTime = SystemAPI.Time.DeltaTime,
            }.ScheduleParallel(state.Dependency);
        }
    }

    /// <summary>
    /// 투사체의 가속도를 속력에 반영하고 진행 방향 속도를 정함.
    /// </summary>
    [BurstCompile]
    [WithAll(typeof(StraightforwardProjectileComponent), typeof(Simulate))]
    [WithNone(typeof(LocalProjectileTag))]
    public partial struct StraightforwardProjectileJob : IJobEntity
    {
        public float DeltaTime;

        private void Execute(
            in LocalTransform transform,
            ref StraightforwardProjectileComponent projectile,
            ref SharedProjectileComponent shared,
            ref MovementComponent movement)
        {
            // 명중으로 끝난 투사체는 맞은 자리에 멈춤.
            if (shared.Dead)
            {
                movement.Velocity = float2.zero;
                shared.Displacement = float2.zero;
                return;
            }

            // 감속 투사체가 뒤로 가지 않도록 속도는 0 이상으로 제한.
            projectile.Speed = math.max(projectile.Speed + projectile.Acceleration * DeltaTime, 0.0F);

            movement.Velocity = transform.Up().xy * projectile.Speed;
            shared.Displacement = movement.Velocity * DeltaTime;
        }
    }
}
