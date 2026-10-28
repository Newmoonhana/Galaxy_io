using GalaxyIO.DOTS.Components.Actor;
using GalaxyIO.DOTS.Components.Message;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.NetCode;

namespace GalaxyIO.DOTS.Systems
{
    /// <summary>
    /// <see cref="ScoreMessage"/>를 플레이어 점수에 반영하고, 바뀐 점수를 모든 클라이언트에 알리는 시스템.
    /// 입장한 플레이어에게는 기존 점수를 모두 보냄.
    /// </summary>
    [BurstCompile]
    [WorldSystemFilter(WorldSystemFilterFlags.ServerSimulation)]
    [UpdateInGroup(typeof(EventSystemGroup))]
    [UpdateAfter(typeof(SpaceshipDeathSystem))]
    public partial struct PlayerScoreSystem : ISystem
    {
        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<NetworkStreamDriver>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var ecb = new EntityCommandBuffer(Allocator.Temp);
            var disconnected = new NativeHashSet<int>(4, Allocator.Temp);

            SendLeaves(ref state, ecb, disconnected);
            ApplyMessages(ref state, ecb);
            SendJoins(ref state, ecb, disconnected);

            ecb.Playback(state.EntityManager);
            ecb.Dispose();
            disconnected.Dispose();
        }

        private void SendLeaves(ref SystemState state, EntityCommandBuffer ecb, NativeHashSet<int> disconnected)
        {
            var driver = SystemAPI.GetSingleton<NetworkStreamDriver>();

            foreach (NetCodeConnectionEvent connectionEvent in driver.ConnectionEventsForTick)
            {
                if (connectionEvent.State != ConnectionState.State.Disconnected)
                {
                    continue;
                }

                disconnected.Add(connectionEvent.Id.Value);

                Send(ecb, new PlayerScoreRPC { NetworkId = connectionEvent.Id.Value, Left = true }, Entity.Null);
            }
        }

        private void ApplyMessages(ref SystemState state, EntityCommandBuffer ecb)
        {
            EntityQuery messageQuery = SystemAPI.QueryBuilder().WithAll<ScoreMessage>().Build();

            if (messageQuery.IsEmpty)
            {
                return;
            }

            var players = new NativeHashMap<int, Entity>(8, Allocator.Temp);

            foreach (
                var (networkId, connection) in
                SystemAPI.Query<RefRO<NetworkId>>()
                    .WithAll<PlayerScoreComponent>()
                    .WithEntityAccess()
            ) {
                players[networkId.ValueRO.Value] = connection;
            }

            var changed = new NativeHashSet<int>(4, Allocator.Temp);

            // 같은 틱에 득점과 사망이 겹치면 사망이 이기도록 Add 를 먼저 반영함.
            Apply(ref state, ScoreOperation.Add, players, changed);
            Apply(ref state, ScoreOperation.Set, players, changed);

            foreach (int networkId in changed)
            {
                Send(ecb, MakeScore(ref state, players[networkId]), Entity.Null);
            }

            state.EntityManager.DestroyEntity(messageQuery);

            changed.Dispose();
            players.Dispose();
        }

        private void Apply(
            ref SystemState state,
            ScoreOperation operation,
            in NativeHashMap<int, Entity> players,
            NativeHashSet<int> changed)
        {
            foreach (var message in SystemAPI.Query<RefRO<ScoreMessage>>())
            {
                // 접속을 끊었거나 아직 입장하지 않은 플레이어는 무시.
                if (message.ValueRO.Operation != operation
                    || !players.TryGetValue(message.ValueRO.NetworkId, out Entity connection))
                {
                    continue;
                }

                RefRW<PlayerScoreComponent> score = SystemAPI.GetComponentRW<PlayerScoreComponent>(connection);

                score.ValueRW.Value = operation == ScoreOperation.Add
                    ? score.ValueRO.Value + message.ValueRO.Value
                    : message.ValueRO.Value;

                changed.Add(message.ValueRO.NetworkId);
            }
        }

        private void SendJoins(ref SystemState state, EntityCommandBuffer ecb, in NativeHashSet<int> disconnected)
        {
            EntityQuery joinQuery = SystemAPI.QueryBuilder()
                .WithAll<NetworkId, PlayerNameComponent, NetworkStreamInGame>()
                .WithNone<PlayerScoreComponent>()
                .Build();

            if (joinQuery.IsEmpty)
            {
                return;
            }

            // 입장 전부터 있던 플레이어들의 점수
            var scores = new NativeList<PlayerScoreRPC>(8, Allocator.Temp);

            EntityQuery playerQuery = SystemAPI.QueryBuilder()
                .WithAll<NetworkId, PlayerNameComponent, PlayerScoreComponent>()
                .Build();

            foreach (Entity connection in playerQuery.ToEntityArray(Allocator.Temp))
            {
                PlayerScoreRPC score = MakeScore(ref state, connection);

                if (!disconnected.Contains(score.NetworkId))
                {
                    scores.Add(score);
                }
            }

            foreach (Entity joined in joinQuery.ToEntityArray(Allocator.Temp))
            {
                for (int i = 0; i < scores.Length; i++)
                {
                    Send(ecb, scores[i], joined);
                }

                ecb.AddComponent(joined, new PlayerScoreComponent());

                Send(ecb, new PlayerScoreRPC
                {
                    Name      = SystemAPI.GetComponent<PlayerNameComponent>(joined).Value,
                    NetworkId = SystemAPI.GetComponent<NetworkId>(joined).Value,
                }, Entity.Null);
            }

            scores.Dispose();
        }

        private PlayerScoreRPC MakeScore(ref SystemState state, Entity connection)
        {
            return new PlayerScoreRPC
            {
                Name      = SystemAPI.GetComponent<PlayerNameComponent>(connection).Value,
                NetworkId = SystemAPI.GetComponent<NetworkId>(connection).Value,
                Score     = SystemAPI.GetComponent<PlayerScoreComponent>(connection).Value,
            };
        }

        // Entity.Null 이면 모든 접속에 전송.
        private static void Send(EntityCommandBuffer ecb, in PlayerScoreRPC message, Entity target)
        {
            Entity rpcEntity = ecb.CreateEntity();

            ecb.AddComponent(rpcEntity, message);
            ecb.AddComponent(rpcEntity, new SendRpcCommandRequest { TargetConnection = target });
        }
    }
}
