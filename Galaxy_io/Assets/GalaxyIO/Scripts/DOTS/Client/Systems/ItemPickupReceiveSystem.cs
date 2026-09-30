using GalaxyIO.DOTS.Client.Components.Message;
using GalaxyIO.DOTS.Components.Message;
using GalaxyIO.DOTS.Components.Singleton;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.NetCode;

namespace GalaxyIO.DOTS.Client.Systems
{
    /// <summary>
    /// 아이템 줍기 알림을 받아 줍기 효과음을 요청하는 시스템.
    /// </summary>
    [BurstCompile]
    [WorldSystemFilter(WorldSystemFilterFlags.ClientSimulation)]
    public partial struct ItemPickupReceiveSystem : ISystem
    {
        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<ItemConfigComponent>();
            state.RequireForUpdate<ItemPickedUpRPC>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            ItemConfigComponent config = SystemAPI.GetSingleton<ItemConfigComponent>();

            var ecb = new EntityCommandBuffer(Allocator.Temp);

            foreach (
                var (pickup, rpcEntity) in
                SystemAPI.Query<RefRO<ItemPickedUpRPC>>()
                    .WithAll<ReceiveRpcCommandRequest>()
                    .WithEntityAccess()
            ) {
                if (config.PickupSound != default)
                {
                    ecb.AddComponent(ecb.CreateEntity(), new SoundMessage
                    {
                        Clip     = config.PickupSound,
                        Position = pickup.ValueRO.Position,
                    });
                }

                ecb.DestroyEntity(rpcEntity);
            }

            ecb.Playback(state.EntityManager);
            ecb.Dispose();
        }
    }
}
