using GalaxyIO.DOTS.Components.Actor;
using GalaxyIO.DOTS.Components.Message;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.NetCode;
using Unity.Transforms;

namespace GalaxyIO.DOTS.Systems
{
    /// <summary>
    /// 플레이어 우주선 위치를 같은 파티원에게 주기적으로 보내는 서버 시스템.
    /// </summary>
    [WorldSystemFilter(WorldSystemFilterFlags.ServerSimulation)]
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    public partial struct PartyPositionSystem : ISystem
    {
        // 보내는 간격(초)
        private const double Interval = 0.25;

        private struct Ship
        {
            public int NetworkId;
            public uint Party;
            public float2 Position;
        }

        private double nextSendTime;

        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<NetworkStreamInGame>();
        }

        public void OnUpdate(ref SystemState state)
        {
            double time = SystemAPI.Time.ElapsedTime;

            if (time < nextSendTime)
            {
                return;
            }

            nextSendTime = time + Interval;

            var ships = new NativeList<Ship>(Allocator.Temp);

            foreach (var (owner, faction, transform) in
                     SystemAPI.Query<RefRO<GhostOwner>, RefRO<FactionComponent>, RefRO<LocalTransform>>()
                         .WithAll<PlayerSpaceshipMovementComponent>())
            {
                // 파티가 없는 플레이어는 혼자만의 번호라 받을 사람이 없음.
                if (faction.ValueRO.Party != Faction.SoloParty(owner.ValueRO.NetworkId))
                {
                    ships.Add(new Ship { NetworkId = owner.ValueRO.NetworkId, Party = faction.ValueRO.Party, Position = transform.ValueRO.Position.xy });
                }
            }

            if (ships.Length > 1)
            {
                var commands = new EntityCommandBuffer(Allocator.Temp);

                foreach (var (networkId, connection) in
                         SystemAPI.Query<RefRO<NetworkId>>().WithAll<NetworkStreamInGame>().WithEntityAccess())
                {
                    uint party = 0;

                    foreach (Ship ship in ships)
                    {
                        if (ship.NetworkId == networkId.ValueRO.Value)
                        {
                            party = ship.Party;
                            break;
                        }
                    }

                    if (party == 0)
                    {
                        continue;
                    }

                    foreach (Ship ship in ships)
                    {
                        if (ship.Party != party || ship.NetworkId == networkId.ValueRO.Value)
                        {
                            continue;
                        }

                        Entity rpc = commands.CreateEntity();
                        commands.AddComponent(rpc, new PartyMemberPositionRPC { NetworkId = ship.NetworkId, Position = ship.Position });
                        commands.AddComponent(rpc, new SendRpcCommandRequest { TargetConnection = connection });
                    }
                }

                commands.Playback(state.EntityManager);
                commands.Dispose();
            }

            ships.Dispose();
        }
    }
}