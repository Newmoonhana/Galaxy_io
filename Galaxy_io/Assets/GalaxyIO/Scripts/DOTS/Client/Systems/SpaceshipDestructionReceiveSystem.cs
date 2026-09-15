using GalaxyIO.DOTS.Client.Components.Message;
using GalaxyIO.DOTS.Components.Message;
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
    /// 우주선 파괴 알림을 받아 폭발 연출을 스폰하고 리스폰 창 태그를 남기는 시스템.
    /// </summary>
    [BurstCompile]
    [WorldSystemFilter(WorldSystemFilterFlags.ClientSimulation)]
    public partial struct SpaceshipDestructionReceiveSystem : ISystem
    {
        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<NetworkId>();
            state.RequireForUpdate<PrefabRegistryElement>();
            state.RequireForUpdate<SpaceshipDestroyedRPC>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            int localNetworkId = SystemAPI.GetSingleton<NetworkId>().Value;

            Entity explosionPrefab = SystemAPI
                .GetSingletonBuffer<PrefabRegistryElement>(true)
                .Find(PrefabId.ExplosionEffect);

            // 프리팹의 회전/크기는 그대로 쓰고 위치만 바꿈.
            LocalTransform explosionTransform = explosionPrefab == Entity.Null
                ? LocalTransform.Identity
                : SystemAPI.GetComponent<LocalTransform>(explosionPrefab);

            var ecb = new EntityCommandBuffer(Allocator.Temp);

            foreach (
                var (destroyed, rpcEntity) in
                SystemAPI.Query<RefRO<SpaceshipDestroyedRPC>>()
                    .WithAll<ReceiveRpcCommandRequest>()
                    .WithEntityAccess()
            ) {
                // 레지스트리에 연출이 없어도 리스폰 처리는 계속함.
                if (explosionPrefab != Entity.Null)
                {
                    Entity explosion = ecb.Instantiate(explosionPrefab);

                    explosionTransform.Position = new float3(destroyed.ValueRO.Position, 0.0F);
                    ecb.SetComponent(explosion, explosionTransform);
                }

                if (destroyed.ValueRO.OwnerNetworkId == localNetworkId)
                {
                    Entity death = ecb.CreateEntity();
                    ecb.AddComponent<PlayerDeathTag>(death);
                }

                ecb.DestroyEntity(rpcEntity);
            }

            ecb.Playback(state.EntityManager);
            ecb.Dispose();
        }
    }
}