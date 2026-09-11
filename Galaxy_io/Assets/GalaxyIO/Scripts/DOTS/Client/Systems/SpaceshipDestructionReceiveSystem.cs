using GalaxyIO.DOTS.Client.Components;
using GalaxyIO.DOTS.Components;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.NetCode;

namespace GalaxyIO.DOTS.Client.Systems
{
    /// <summary>
    /// 우주선 파괴 알림을 받아 연출 이벤트와 리스폰 창 태그로 옮기는 시스템.
    /// </summary>
    [BurstCompile]
    [WorldSystemFilter(WorldSystemFilterFlags.ClientSimulation)]
    public partial struct SpaceshipDestructionReceiveSystem : ISystem
    {
        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<NetworkId>();
            state.RequireForUpdate<SpaceshipDestroyedRPC>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            int localNetworkId = SystemAPI.GetSingleton<NetworkId>().Value;

            var ecb = new EntityCommandBuffer(Allocator.Temp);

            foreach (
                var (destroyed, rpcEntity) in
                SystemAPI.Query<RefRO<SpaceshipDestroyedRPC>>()
                    .WithAll<ReceiveRpcCommandRequest>()
                    .WithEntityAccess()
            ) {
                // 연출은 MonoBehaviour 가 재생함. 위치만 남겨 둠.
                Entity effect = ecb.CreateEntity();

                ecb.AddComponent(
                    effect,
                    new DeathEffectEvent
                    {
                        Position = destroyed.ValueRO.Position,
                    }
                );

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
