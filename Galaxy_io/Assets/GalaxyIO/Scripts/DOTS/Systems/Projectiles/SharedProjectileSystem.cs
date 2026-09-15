using GalaxyIO.DOTS.Components.Actor;
using Unity.Burst;
using Unity.Entities;
using Unity.NetCode;

namespace GalaxyIO.DOTS.Systems
{
    /// <summary>
    /// 투사체의 남은 지속시간을 줄이고 만료된 투사체를 제거하는 시스템.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(DespawnSystemGroup))]
    public partial struct SharedProjectileSystem : ISystem
    {
        private bool isServer;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            isServer = state.WorldUnmanaged.IsServer();

            state.RequireForUpdate<NetworkTime>();
            state.RequireForUpdate<EndPredictedSimulationEntityCommandBufferSystem.Singleton>();
            state.RequireForUpdate(SystemAPI.QueryBuilder().WithAll<SharedProjectileComponent>().Build());
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            // 롤백 재시뮬레이션에서 수명이 여러 번 깎이지 않도록 틱당 한 번만 실행.
            // 서버에서는 이 플래그가 항상 켜져 있음.
            if (!SystemAPI.GetSingleton<NetworkTime>().IsFirstTimeFullyPredictingTick)
            {
                return;
            }

            state.Dependency = new SharedProjectileJob
            {
                DeltaTime = SystemAPI.Time.DeltaTime,
                Authoritative = isServer,
                CommandBuffer = SystemAPI
                    .GetSingleton<EndPredictedSimulationEntityCommandBufferSystem.Singleton>()
                    .CreateCommandBuffer(state.WorldUnmanaged).AsParallelWriter(),
            }.ScheduleParallel(state.Dependency);
        }
    }

    /// <summary>
    /// 투사체 지속시간 감소 및 만료 판정.
    /// </summary>
    [BurstCompile]
    [WithAll(typeof(SharedProjectileComponent), typeof(Simulate))]
    public partial struct SharedProjectileJob : IJobEntity
    {
        public float DeltaTime;

        // 클라이언트가 Ghost를 지우면 다음 스냅샷에서 되살아나며 에러가 남. 서버만 지움.
        public bool Authoritative;

        public EntityCommandBuffer.ParallelWriter CommandBuffer;

        private void Execute(Entity entity, [ChunkIndexInQuery] int chunkIndex, ref SharedProjectileComponent sharedProjectileComponent)
        {
            sharedProjectileComponent.Lifetime -= DeltaTime;

            // 수명이 다한 투사체는 제거. 병렬 실행이라 ECB로 미뤄서 처리함.
            if (sharedProjectileComponent.Lifetime <= 0.0F && Authoritative)
            {
                CommandBuffer.DestroyEntity(chunkIndex, entity);
            }
        }
    }
}