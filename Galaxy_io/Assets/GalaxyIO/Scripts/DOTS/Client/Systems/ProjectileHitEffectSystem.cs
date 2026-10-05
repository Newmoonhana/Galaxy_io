using GalaxyIO.DOTS.Client.Components.Message;
using GalaxyIO.DOTS.Components.Actor;
using GalaxyIO.DOTS.Components.Singleton;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace GalaxyIO.DOTS.Client.Systems
{
    /// <summary>
    /// 서버가 복제한 투사체 명중 수가 늘어나면 마지막 명중 지점에 명중 연출/효과음을 스폰하는 시스템.
    /// </summary>
    [BurstCompile]
    [WorldSystemFilter(WorldSystemFilterFlags.ClientSimulation)]
    public partial struct ProjectileHitEffectSystem : ISystem
    {
        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<PrefabRegistryElement>();
            state.RequireForUpdate(SystemAPI.QueryBuilder().WithAll<SharedProjectileComponent>().Build());
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            DynamicBuffer<PrefabRegistryElement> registry = SystemAPI.GetSingletonBuffer<PrefabRegistryElement>(true);

            var ecb = new EntityCommandBuffer(Allocator.Temp);

            foreach (var shared in SystemAPI.Query<RefRW<SharedProjectileComponent>>())
            {
                if (shared.ValueRO.PresentedHitCount == shared.ValueRO.HitCount)
                {
                    continue;
                }

                shared.ValueRW.PresentedHitCount = shared.ValueRO.HitCount;

                Entity prefab = registry.Find(shared.ValueRO.SpawnOnHit);

                if (prefab == Entity.Null)
                {
                    continue;
                }

                float2 position = shared.ValueRO.HitPosition;

                // 프리팹의 회전/크기는 그대로 쓰고 위치만 바꿈.
                LocalTransform effectTransform = SystemAPI.GetComponent<LocalTransform>(prefab);
                effectTransform.Position = new float3(position, 0.0F);

                Entity effect = ecb.Instantiate(prefab);
                ecb.SetComponent(effect, effectTransform);

                if (SystemAPI.HasComponent<EffectSoundComponent>(prefab))
                {
                    ecb.AddComponent(ecb.CreateEntity(), new SoundMessage
                    {
                        SoundKey = SystemAPI.GetComponent<EffectSoundComponent>(prefab).SoundKey,
                        Position = position,
                    });
                }
            }

            ecb.Playback(state.EntityManager);
            ecb.Dispose();
        }
    }
}
