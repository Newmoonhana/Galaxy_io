using GalaxyIO.DOTS.Client.Components.Message;
using GalaxyIO.DOTS.Components.Message;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.NetCode;

namespace GalaxyIO.DOTS.Client.Systems
{
    /// <summary>
    /// 점수 RPC 를 받아 점수판 뷰가 읽을 메시지로 남기는 시스템.
    /// </summary>
    [BurstCompile]
    [WorldSystemFilter(WorldSystemFilterFlags.ClientSimulation)]
    public partial struct ScoreboardReceiveSystem : ISystem
    {
        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<PlayerScoreRPC>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var ecb = new EntityCommandBuffer(Allocator.Temp);

            foreach (
                var (score, rpcEntity) in
                SystemAPI.Query<RefRO<PlayerScoreRPC>>()
                    .WithAll<ReceiveRpcCommandRequest>()
                    .WithEntityAccess()
            ) {
                Entity message = ecb.CreateEntity();

                ecb.AddComponent(message, new ScoreboardMessage
                {
                    Name      = score.ValueRO.Name,
                    NetworkId = score.ValueRO.NetworkId,
                    Score     = score.ValueRO.Score,
                    Left      = score.ValueRO.Left,
                });

                ecb.DestroyEntity(rpcEntity);
            }

            ecb.Playback(state.EntityManager);
            ecb.Dispose();
        }
    }
}
