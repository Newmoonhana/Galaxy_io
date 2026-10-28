using GalaxyIO.DOTS.Components.Actor;
using GalaxyIO.DOTS.Components.Message;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.NetCode;

namespace GalaxyIO.DOTS.Systems
{
    /// <summary>
    /// 이번 틱에 쌓인 <see cref="DamageReportElement"/>를 때린 플레이어와 맞은 플레이어에게 보내는 시스템.
    /// </summary>
    [BurstCompile]
    [WorldSystemFilter(WorldSystemFilterFlags.ServerSimulation)]
    [UpdateInGroup(typeof(EventSystemGroup))]
    [UpdateAfter(typeof(ProjectileDamageSystem))]
    [UpdateAfter(typeof(SpaceshipCollisionSystem))]
    public partial struct DamageNotifySystem : ISystem
    {
        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate(SystemAPI.QueryBuilder().WithAll<DamageReportElement, GhostOwner>().Build());
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            // NetworkId -> 접속
            var connections = new NativeHashMap<int, Entity>(4, Allocator.Temp);

            foreach (
                var (networkId, connection) in
                SystemAPI.Query<RefRO<NetworkId>>()
                    .WithAll<NetworkStreamInGame>()
                    .WithEntityAccess()
            ) {
                connections[networkId.ValueRO.Value] = connection;
            }

            var ecb = new EntityCommandBuffer(Allocator.Temp);

            foreach (
                var (reports, owner, entity) in
                SystemAPI.Query<DynamicBuffer<DamageReportElement>, RefRO<GhostOwner>>()
                    .WithEntityAccess()
            ) {
                for (int i = 0; i < reports.Length; i++)
                {
                    DamageReportElement report = reports[i];

                    var message = new DamageRPC
                    {
                        Spaceship = entity,
                        Position  = report.Position,
                        Amount    = report.Amount,
                        Turret    = report.Turret,
                    };

                    // AI(-1)는 받을 접속이 없음. 자기 자신은 맞은 쪽으로만 받음.
                    if (report.AttackerNetworkId != owner.ValueRO.NetworkId
                        && connections.TryGetValue(report.AttackerNetworkId, out Entity attacker))
                    {
                        message.Dealt = true;
                        Send(ecb, message, attacker);
                    }

                    if (connections.TryGetValue(owner.ValueRO.NetworkId, out Entity victim))
                    {
                        message.Dealt = false;
                        Send(ecb, message, victim);
                    }
                }

                reports.Clear();
            }

            ecb.Playback(state.EntityManager);
            ecb.Dispose();
            connections.Dispose();
        }

        private static void Send(EntityCommandBuffer ecb, in DamageRPC message, Entity target)
        {
            Entity rpcEntity = ecb.CreateEntity();

            ecb.AddComponent(rpcEntity, message);
            ecb.AddComponent(rpcEntity, new SendRpcCommandRequest { TargetConnection = target });
        }
    }
}
