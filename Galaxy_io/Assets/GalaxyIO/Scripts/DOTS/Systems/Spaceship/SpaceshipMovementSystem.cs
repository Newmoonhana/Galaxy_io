using GalaxyIO.DOTS.Components;
using Unity.Burst;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace GalaxyIO.DOTS.Systems
{
    [BurstCompile]
    public partial struct SpaceshipMovementSystem : ISystem
    {
        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate(SystemAPI.QueryBuilder().WithAll<SpaceshipMovementComponent>().Build());
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            state.Dependency = new MoveJob
            {
                DeltaTime = SystemAPI.Time.DeltaTime,
            }.ScheduleParallel(state.Dependency);
        }
    }

    [BurstCompile]
    [WithAll(typeof(SpaceshipMovementComponent))]
    public partial struct MoveJob : IJobEntity
    {
        public float DeltaTime;

        private void Execute(ref LocalTransform transform, ref SpaceshipMovementComponent spaceshipSpaceshipMovement)
        {
            float len = math.length(spaceshipSpaceshipMovement.Forward);
            float sca = spaceshipSpaceshipMovement.Speed / len;
            float2 Velocity = spaceshipSpaceshipMovement.Forward * sca;

            transform.Position = new float3(transform.Position.xy + Velocity * DeltaTime, transform.Position.z);

            if (len > math.EPSILON)
            {
                transform.Rotation = quaternion.RotateZ(
                    -math.atan2(spaceshipSpaceshipMovement.Forward.x, spaceshipSpaceshipMovement.Forward.y)
                );
            }
        }
    }
}