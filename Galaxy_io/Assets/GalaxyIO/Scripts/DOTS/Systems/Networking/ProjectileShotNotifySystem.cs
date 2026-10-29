using GalaxyIO.DOTS.Components.Actor;
using GalaxyIO.DOTS.Components.Message;
using GalaxyIO.DOTS.Components.Singleton;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.NetCode;

namespace GalaxyIO.DOTS.Systems
{
    /// <summary>
    /// 이번 틱에 쌓인 <see cref="ProjectileShotMessage"/>를 근처 접속에 <see cref="ProjectileShotRPC"/>로 보내는 시스템.
    /// 복제 범위에 투사체가 날아가는 거리를 더한 범위 안의 접속만 받음. 쏜 플레이어는 직접 쏘므로 받지 않음.
    /// </summary>
    [BurstCompile]
    [WorldSystemFilter(WorldSystemFilterFlags.ServerSimulation)]
    [UpdateInGroup(typeof(EventSystemGroup))]
    public partial struct ProjectileShotNotifySystem : ISystem
    {
        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<NetworkTime>();
            state.RequireForUpdate<RelevancyConfigComponent>();
            state.RequireForUpdate<ProjectileShotMessage>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            DynamicBuffer<ProjectileShotMessage> shots = SystemAPI.GetSingletonBuffer<ProjectileShotMessage>();

            if (shots.IsEmpty)
            {
                return;
            }

            RelevancyConfigComponent config = SystemAPI.GetSingleton<RelevancyConfigComponent>();
            uint tick = SystemAPI.GetSingleton<NetworkTime>().ServerTick.SerializedData;

            var focuses = new NativeList<RelevancyFocus>(4, Allocator.Temp);

            foreach (
                var (position, radius, networkId, connection) in
                SystemAPI.Query<RefRO<GhostConnectionPosition>, RefRO<RelevancyRadiusComponent>, RefRO<NetworkId>>()
                    .WithAll<NetworkStreamInGame>()
                    .WithEntityAccess()
            ) {
                focuses.Add(new RelevancyFocus
                {
                    Connection = connection,
                    NetworkId  = networkId.ValueRO.Value,
                    Position   = position.ValueRO.Position.xy,

                    // 바깥 반경
                    Radius = radius.ValueRO.Value + config.Hysteresis,
                });
            }

            var ecb = new EntityCommandBuffer(Allocator.Temp);

            foreach (ProjectileShotMessage message in shots)
            {
                ProjectileShotRPC shot = message.Shot;
                shot.Tick = tick;

                for (int i = 0; i < focuses.Length; i++)
                {
                    float range = focuses[i].Radius + message.Reach;

                    if (focuses[i].NetworkId == message.NetworkId
                        || math.distancesq(focuses[i].Position, shot.Position) > range * range)
                    {
                        continue;
                    }

                    Entity rpcEntity = ecb.CreateEntity();
                    ecb.AddComponent(rpcEntity, shot);
                    ecb.AddComponent(rpcEntity, new SendRpcCommandRequest { TargetConnection = focuses[i].Connection });
                }
            }

            shots.Clear();

            ecb.Playback(state.EntityManager);
            ecb.Dispose();
            focuses.Dispose();
        }
    }
}
