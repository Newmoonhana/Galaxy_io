using System.Collections.Generic;
using System.Linq;
using GalaxyIO.DOTS.Components.Actor;
using GalaxyIO.DOTS.Components.Message;
using GalaxyIO.DOTS.Components.Singleton;
using Unity.Collections;
using Unity.Entities;
using Unity.NetCode;

namespace GalaxyIO.DOTS.Systems
{
    /// <summary>
    /// 받은 <see cref="PingRPC"/>를 보낸 사람을 포함한 파티원에게 중계하는 서버 시스템.
    /// 연속으로 찍으면 일정 시간 막음.
    /// </summary>
    [WorldSystemFilter(WorldSystemFilterFlags.ServerSimulation)]
    [UpdateInGroup(typeof(SimulationSystemGroup), OrderFirst = true)]
    [UpdateAfter(typeof(GhostSimulationSystemGroup))]
    [UpdateBefore(typeof(PredictedSimulationSystemGroup))]
    public sealed partial class PingSystem : SystemBase
    {
        // 이 시간(초) 안에 MaxPings 개를 넘게 찍으면 BlockDuration 동안 막음.
        private const double SpamWindow = 3.0;
        private const int MaxPings = 5;
        private const double BlockDuration = 6.0;

        // 연속 핑 세기. 세는 중이 아닐 때 찍으면 새로 세기 시작함.
        private struct SpamCounter
        {
            public double StartTime;
            public int Count;

            // 이 시각까지 새 핑을 받지 않음.
            public double BlockedUntil;
        }

        private EntityQuery requestQuery;
        private EntityQuery playerQuery;

        // NetworkId -> 연속 핑 세기
        private readonly Dictionary<int, SpamCounter> counters = new Dictionary<int, SpamCounter>();

        protected override void OnCreate()
        {
            requestQuery = SystemAPI.QueryBuilder().WithAll<ReceiveRpcCommandRequest, PingRPC>().Build();

            // 입장한 플레이어의 Connection
            playerQuery = SystemAPI.QueryBuilder().WithAll<NetworkId, PlayerNameComponent>().Build();

            RequireForUpdate(requestQuery);
            RequireForUpdate<PartyServiceComponent>();
        }

        protected override void OnUpdate()
        {
            using NativeArray<ReceiveRpcCommandRequest> requests = requestQuery.ToComponentDataArray<ReceiveRpcCommandRequest>(Allocator.Temp);
            using NativeArray<PingRPC> pings = requestQuery.ToComponentDataArray<PingRPC>(Allocator.Temp);
            using NativeArray<Entity> players = playerQuery.ToEntityArray(Allocator.Temp);
            using NativeArray<NetworkId> playerIds = playerQuery.ToComponentDataArray<NetworkId>(Allocator.Temp);

            EntityManager.DestroyEntity(requestQuery);

            var party = SystemAPI.ManagedAPI.GetSingleton<PartyServiceComponent>().Value;
            double time = SystemAPI.Time.ElapsedTime;

            for (int i = 0; i < pings.Length; i++)
            {
                // 입장 전이거나 보낸 뒤 접속이 끊긴 요청은 버림.
                Entity sender = requests[i].SourceConnection;

                if (!EntityManager.HasComponent<PlayerNameComponent>(sender))
                {
                    continue;
                }

                PingRPC ping = pings[i];
                ping.Sender = EntityManager.GetComponentData<PlayerNameComponent>(sender).Value;
                ping.SenderNetworkId = EntityManager.GetComponentData<NetworkId>(sender).Value;

                // 지우기는 횟수와 관계없이 받음.
                if (!ping.Remove)
                {
                    counters.TryGetValue(ping.SenderNetworkId, out SpamCounter counter);

                    if (time < counter.BlockedUntil)
                    {
                        continue;
                    }

                    if (counter.Count == 0 || time - counter.StartTime >= SpamWindow)
                    {
                        counter = new SpamCounter { StartTime = time };
                    }

                    counter.Count++;

                    // 연속 핑을 찾은 시점부터 막음. 막힌 뒤에는 새로 세기 시작함.
                    if (counter.Count > MaxPings)
                    {
                        counters[ping.SenderNetworkId] = new SpamCounter { BlockedUntil = time + BlockDuration };
                        Send(new ChatRPC { Channel = ChatChannel.System, Text = "Ping/TooMany" }, sender);
                        continue;
                    }

                    counters[ping.SenderNetworkId] = counter;
                }

                // 파티가 없으면 자신에게만 보냄.
                IReadOnlyList<int> members = party.Members(ping.SenderNetworkId);

                if (members == null)
                {
                    Send(ping, sender);
                    continue;
                }

                for (int j = 0; j < players.Length; j++)
                {
                    if (members.Contains(playerIds[j].Value))
                    {
                        Send(ping, players[j]);
                    }
                }
            }
        }

        private void Send<T>(in T rpc, Entity target) where T : unmanaged, IRpcCommand
        {
            Entity entity = EntityManager.CreateEntity();
            EntityManager.AddComponentData(entity, rpc);
            EntityManager.AddComponentData(entity, new SendRpcCommandRequest { TargetConnection = target });
        }
    }
}