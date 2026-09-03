using GalaxyIO.DOTS.Components;
using Unity.Burst;
using Unity.Entities;

namespace GalaxyIO.DOTS.Systems
{
    [BurstCompile]
    [UpdateInGroup(typeof(DespawnSystemGroup))]
    public partial struct SharedProjectileSystem : ISystem
    {
        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<EndSimulationEntityCommandBufferSystem.Singleton>();
            state.RequireForUpdate(SystemAPI.QueryBuilder().WithAll<SharedProjectileComponent>().Build());
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            state.Dependency = new SharedProjectileJob
            {
                DeltaTime = SystemAPI.Time.DeltaTime,
                CommandBuffer = SystemAPI
                    .GetSingleton<EndSimulationEntityCommandBufferSystem.Singleton>()
                    .CreateCommandBuffer(state.WorldUnmanaged).AsParallelWriter(),
            }.ScheduleParallel(state.Dependency);
        }
    }

    [BurstCompile]
    [WithAll(typeof(SharedProjectileComponent))]
    public partial struct SharedProjectileJob : IJobEntity
    {
        public float DeltaTime;
        public EntityCommandBuffer.ParallelWriter CommandBuffer;

        private void Execute(Entity entity, [ChunkIndexInQuery] int chunkIndex, ref SharedProjectileComponent sharedProjectileComponent)
        {
            sharedProjectileComponent.Lifetime -= DeltaTime;

            if (sharedProjectileComponent.Lifetime <= 0.0F)
            {
                CommandBuffer.DestroyEntity(chunkIndex, entity);
            }
        }
    }
}