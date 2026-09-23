using GalaxyIO.DOTS.Client.Components.Message;
using GalaxyIO.DOTS.Components.Message;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.NetCode;

namespace GalaxyIO.DOTS.Client.Systems
{
    /// <summary>
    /// 킬 로그 RPC 를 받아 킬 로그 뷰가 읽을 메시지로 남기는 시스템.
    /// </summary>
    [BurstCompile]
    [WorldSystemFilter(WorldSystemFilterFlags.ClientSimulation)]
    public partial struct KillLogReceiveSystem : ISystem
    {
        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<NetworkId>();
            state.RequireForUpdate<KillLogRPC>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            int localNetworkId = SystemAPI.GetSingleton<NetworkId>().Value;

            var ecb = new EntityCommandBuffer(Allocator.Temp);

            foreach (
                var (log, rpcEntity) in
                SystemAPI.Query<RefRO<KillLogRPC>>()
                    .WithAll<ReceiveRpcCommandRequest>()
                    .WithEntityAccess()
            ) {
                Entity message = ecb.CreateEntity();

                ecb.AddComponent(message, new KillLogMessage
                {
                    Killer        = log.ValueRO.Killer,
                    Assistant     = log.ValueRO.Assistant,
                    Victim        = log.ValueRO.Victim,
                    Type          = log.ValueRO.Type,
                    KillerIsEnemy = log.ValueRO.KillerNetworkId == 0,
                    IsLocal       = log.ValueRO.KillerNetworkId == localNetworkId
                                    || log.ValueRO.AssistantNetworkId == localNetworkId
                                    || log.ValueRO.VictimNetworkId == localNetworkId,
                });

                ecb.DestroyEntity(rpcEntity);
            }

            ecb.Playback(state.EntityManager);
            ecb.Dispose();
        }
    }
}
