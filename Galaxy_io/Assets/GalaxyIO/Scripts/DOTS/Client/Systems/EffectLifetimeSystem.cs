using GalaxyIO.DOTS.Components.Actor;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;

namespace GalaxyIO.DOTS.Client.Systems
{
    /// <summary>
    /// 재생이 끝난 연출 엔티티를 제거하는 시스템.
    /// </summary>
    [BurstCompile]
    [WorldSystemFilter(WorldSystemFilterFlags.ClientSimulation)]
    public partial struct EffectLifetimeSystem : ISystem
    {
        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<EffectLifetimeComponent>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            float deltaTime = SystemAPI.Time.DeltaTime;

            // Query 순회 중의 구조 변경(Structural Change)은 ECB 로 미뤄서 처리함.
            var ecb = new EntityCommandBuffer(Allocator.Temp);

            // 프리팹 엔티티는 기본 Query 에서 빠지므로 줄어들지 않음.
            foreach (
                var (lifetime, entity) in
                SystemAPI.Query<RefRW<EffectLifetimeComponent>>()
                    .WithEntityAccess()
            ) {
                lifetime.ValueRW.Remaining -= deltaTime;

                if (lifetime.ValueRO.Remaining <= 0.0F)
                {
                    ecb.DestroyEntity(entity);
                }
            }

            ecb.Playback(state.EntityManager);
            ecb.Dispose();
        }
    }
}