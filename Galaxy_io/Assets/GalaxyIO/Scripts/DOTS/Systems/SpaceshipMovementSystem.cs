using GalaxyIO.Components;
using Unity.Burst;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace GalaxyIO.Systems
{
    [BurstCompile]
    public partial struct SpaceshipMovementSystem : ISystem
    {
        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate(SystemAPI.QueryBuilder().WithAll<MovementComponent>().Build());
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
    [WithAll(typeof(MovementComponent))]
    public partial struct MoveJob : IJobEntity
    {
        public float DeltaTime;

        private void Execute(ref LocalTransform transform, ref MovementComponent spaceshipMovement)
        {
            float len = math.length(spaceshipMovement.Forward);
            float sca = spaceshipMovement.Speed / len;
            float2 Velocity = spaceshipMovement.Forward * sca;

            transform.Position = new float3(transform.Position.xy + Velocity * DeltaTime, transform.Position.z);

            if (len > math.EPSILON)
            {
                transform.Rotation = quaternion.RotateZ(
                    -math.atan2(spaceshipMovement.Forward.x, spaceshipMovement.Forward.y)
                );
            }
        }
    }
}