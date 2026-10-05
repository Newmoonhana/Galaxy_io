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
    /// 우주선 충돌 알림을 받아 충돌음을 요청하는 시스템.
    /// </summary>
    [BurstCompile]
    [WorldSystemFilter(WorldSystemFilterFlags.ClientSimulation)]
    public partial struct SpaceshipCrashReceiveSystem : ISystem
    {
        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<CollisionConfigComponent>();
            state.RequireForUpdate<SpaceshipCrashedRPC>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            CollisionConfigComponent config = SystemAPI.GetSingleton<CollisionConfigComponent>();

            var ecb = new EntityCommandBuffer(Allocator.Temp);

            foreach (
                var (crash, rpcEntity) in
                SystemAPI.Query<RefRO<SpaceshipCrashedRPC>>()
                    .WithAll<ReceiveRpcCommandRequest>()
                    .WithEntityAccess()
            ) {
                if (config.CrashSound != default)
                {
                    ecb.AddComponent(ecb.CreateEntity(), new SoundMessage
                    {
                        SoundKey = config.CrashSound,
                        Position = crash.ValueRO.Position,
                    });
                }

                ecb.DestroyEntity(rpcEntity);
            }

            ecb.Playback(state.EntityManager);
            ecb.Dispose();
        }
    }
}
