using GalaxyIO.DOTS.Components;
using Unity.Burst;
using Unity.Entities;

namespace GalaxyIO.DOTS.Systems
{
    /// <summary>
    /// 투사체의 남은 지속시간을 줄이고 만료된 투사체를 제거하는 시스템.
    /// </summary>
    [BurstCompile]
    [WorldSystemFilter(WorldSystemFilterFlags.ServerSimulation)]
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

    /// <summary>
    /// 투사체 지속시간 감소 및 만료 판정.
    /// </summary>
    [BurstCompile]
    [WithAll(typeof(SharedProjectileComponent))]
    public partial struct SharedProjectileJob : IJobEntity
    {
        public float DeltaTime;
        public EntityCommandBuffer.ParallelWriter CommandBuffer;

        private void Execute(Entity entity, [ChunkIndexInQuery] int chunkIndex, ref SharedProjectileComponent sharedProjectileComponent)
        {
            sharedProjectileComponent.Lifetime -= DeltaTime;

            // 수명이 다한 투사체는 제거. 병렬 실행이라 ECB로 미뤄서 처리함.
            if (sharedProjectileComponent.Lifetime <= 0.0F)
            {
                CommandBuffer.DestroyEntity(chunkIndex, entity);
            }
        }
    }
}