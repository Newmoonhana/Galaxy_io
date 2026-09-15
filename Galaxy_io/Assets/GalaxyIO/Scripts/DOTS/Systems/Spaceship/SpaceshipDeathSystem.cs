using GalaxyIO.DOTS.Components.Actor;
using GalaxyIO.DOTS.Components.Message;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.NetCode;
using Unity.Transforms;

namespace GalaxyIO.DOTS.Systems
{
    /// <summary>
    /// 체력이 0이 된 우주선을 제거하고 파괴 사실을 클라이언트에 알리는 시스템.
    /// </summary>
    [BurstCompile]
    [WorldSystemFilter(WorldSystemFilterFlags.ServerSimulation)]
    [UpdateInGroup(typeof(DespawnSystemGroup))]
    public partial struct SpaceshipDeathSystem : ISystem
    {
        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate(
                SystemAPI.QueryBuilder()
                    .WithAll<SpaceshipSpecComponent, LocalTransform, GhostOwner>()
                    .Build()
            );
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            // Query 순회 중의 구조 변경(Structural Change)은 ECB 로 미뤄서 처리함.
            var ecb = new EntityCommandBuffer(Allocator.Temp);

            foreach (
                var (spec, transform, owner, entity) in
                SystemAPI.Query<RefRO<SpaceshipSpecComponent>, RefRO<LocalTransform>, RefRO<GhostOwner>>()
                    .WithEntityAccess()
            ) {
                if (spec.ValueRO.Health > 0.0F)
                {
                    continue;
                }

                Entity rpcEntity = ecb.CreateEntity();

                ecb.AddComponent(
                    rpcEntity,
                    new SpaceshipDestroyedRPC
                    {
                        Position       = transform.ValueRO.Position.xy,
                        OwnerNetworkId = owner.ValueRO.NetworkId,
                    }
                );

                // TargetConnection 을 비우면 접속한 모든 클라이언트로 전송됨.
                ecb.AddComponent<SendRpcCommandRequest>(rpcEntity);

                ecb.DestroyEntity(entity);
            }

            ecb.Playback(state.EntityManager);
            ecb.Dispose();
        }
    }
}