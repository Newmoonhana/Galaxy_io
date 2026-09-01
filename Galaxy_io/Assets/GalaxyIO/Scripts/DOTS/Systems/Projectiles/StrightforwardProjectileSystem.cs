using GalaxyIO.Components.Projectiles;
using Unity.Burst;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace GalaxyIO.Systems
{
    [BurstCompile]
    [UpdateInGroup(typeof(MovementSystemGroup))]
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

    [BurstCompile]
    [WithAll(typeof(StraightforwardProjectileComponent))]
    public partial struct StraightforwardProjectileJob : IJobEntity
    {
        public float DeltaTime;

        private void Execute(ref LocalTransform transform, ref StraightforwardProjectileComponent projectile)
        {
            // 진행 방향
            float2 direction = transform.Up().xy;

            projectile.speed = math.max(projectile.speed + projectile.acceleration * DeltaTime, 0.0F);

            transform.Position = new float3(
                transform.Position.xy + direction * (projectile.speed * DeltaTime),
                transform.Position.z
            );
        }
    }
}