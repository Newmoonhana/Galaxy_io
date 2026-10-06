using GalaxyIO.DOTS.Client.Components.Message;
using GalaxyIO.DOTS.Client.Components.Singleton;
using GalaxyIO.DOTS.Components.Actor;
using GalaxyIO.DOTS.Components.Singleton;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.NetCode;
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
        private EntityQuery localShipQuery;

        // 마지막 리스너 위치. 파괴되는 순간의 자기 폭발음을 살리는 데 씀.
        // 리스폰 대기 중(Dead)에는 이 캐시를 쓰지 않음 - 사망 지점 주변 소리까지 들리면 안 됨.
        private float2 lastListener;
        private bool hasLastListener;
        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<PrefabRegistryElement>();
            state.RequireForUpdate(SystemAPI.QueryBuilder().WithAll<SharedProjectileComponent>().Build());

            localShipQuery = SystemAPI.QueryBuilder().WithAll<GhostOwnerIsLocal, LocalTransform>().Build();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            // 리스너는 로컬 우주선. 살아 있으면 위치를 갱신하고, 사라지면 마지막 위치를 계속 씀.
            if (!localShipQuery.IsEmpty)
            {
                lastListener = localShipQuery.GetSingleton<LocalTransform>().Position.xy;
                hasLastListener = true;
            }

            // 리스폰 대기 중에는 리스너가 없는 것으로 다뤄 전부 컬링함.
            // 자기 폭발음은 Dead 가 켜지기 전 프레임에 만들어지므로 이 분기에 걸리지 않음.
            SystemAPI.TryGetSingleton(out PlayerStateComponent playerState);

            bool hasListener = hasLastListener && !playerState.Dead;
            float2 listener = lastListener;

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

                if (SystemAPI.HasComponent<EffectSoundComponent>(prefab)
                    && SoundAudibleRange.IsAudible(hasListener, listener, position))
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
