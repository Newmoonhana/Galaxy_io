using GalaxyIO.DOTS.Components.Actor;
using GalaxyIO.DOTS.Components.Message;
using GalaxyIO.DOTS.Components.Singleton;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.NetCode;
using Unity.Transforms;

namespace GalaxyIO.DOTS.Systems
{
    /// <summary>
    /// 체력이 0이 된 우주선을 제거하고 파괴 사실을 근처 클라이언트와 소유자에게 알리는 시스템.
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
            // 복제 범위를 쓰면 근처 접속과 소유자에게만 보냄. 끄면 모두에게 보냄.
            bool filtered = SystemAPI.TryGetSingleton(out RelevancyConfigComponent config) && config.UseRelevancy;
            float outerRadius = config.Radius + config.Hysteresis;
            float outerRadiusSq = outerRadius * outerRadius;

            var focuses = new NativeList<RelevancyFocus>(4, Allocator.Temp);

            if (filtered)
            {
                foreach (
                    var (position, networkId, connection) in
                    SystemAPI.Query<RefRO<GhostConnectionPosition>, RefRO<NetworkId>>()
                        .WithAll<NetworkStreamInGame>()
                        .WithEntityAccess()
                ) {
                    focuses.Add(new RelevancyFocus
                    {
                        Connection = connection,
                        NetworkId  = networkId.ValueRO.Value,
                        Position   = position.ValueRO.Position.xy,
                    });
                }
            }

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

                var message = new SpaceshipDestroyedRPC
                {
                    Position       = transform.ValueRO.Position.xy,
                    OwnerNetworkId = owner.ValueRO.NetworkId,
                };

                if (!filtered)
                {
                    Send(ecb, message, Entity.Null);
                }

                for (int i = 0; i < focuses.Length; i++)
                {
                    if (focuses[i].NetworkId == message.OwnerNetworkId
                        || math.distancesq(focuses[i].Position, message.Position) <= outerRadiusSq)
                    {
                        Send(ecb, message, focuses[i].Connection);
                    }
                }

                ecb.DestroyEntity(entity);
            }

            ecb.Playback(state.EntityManager);
            ecb.Dispose();
            focuses.Dispose();
        }

        // TargetConnection 이 Entity.Null 이면 접속한 모든 클라이언트로 전송됨.
        private static void Send(EntityCommandBuffer ecb, in SpaceshipDestroyedRPC message, Entity target)
        {
            Entity rpcEntity = ecb.CreateEntity();

            ecb.AddComponent(rpcEntity, message);
            ecb.AddComponent(rpcEntity, new SendRpcCommandRequest { TargetConnection = target });
        }
    }
}
