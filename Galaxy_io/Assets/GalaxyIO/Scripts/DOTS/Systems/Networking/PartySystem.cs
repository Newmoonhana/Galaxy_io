using GalaxyIO.DOTS.Components.Actor;
using GalaxyIO.DOTS.Components.Message;
using GalaxyIO.DOTS.Components.Singleton;
using GalaxyIO.DOTS.Party;
using Unity.Collections;
using Unity.Entities;
using Unity.NetCode;

namespace GalaxyIO.DOTS.Systems
{
    /// <summary>
    /// <see cref="PartyService"/>와 ECS 를 잇는 시스템. 받은 파티 명령어와 접속 종료를 서비스에 넘기고 서비스를 틱마다 갱신함.
    /// 충돌 판정과 발사 전에 우주선의 파티를 맞춤.
    /// </summary>
    [WorldSystemFilter(WorldSystemFilterFlags.ServerSimulation)]
    [UpdateInGroup(typeof(SimulationSystemGroup), OrderFirst = true)]
    [UpdateAfter(typeof(GhostSimulationSystemGroup))]
    [UpdateBefore(typeof(PredictedSimulationSystemGroup))]
    public partial struct PartySystem : ISystem
    {
        private EntityQuery commandQuery;

        public void OnCreate(ref SystemState state)
        {
            Entity entity = state.EntityManager.CreateEntity();
            state.EntityManager.AddComponentObject(entity, new PartyServiceComponent
            {
                Value = new PartyService(state.EntityManager),
            });

            commandQuery = SystemAPI.QueryBuilder().WithAll<ReceiveRpcCommandRequest, PartyCommandRPC>().Build();

            state.RequireForUpdate<NetworkStreamDriver>();
        }

        public void OnUpdate(ref SystemState state)
        {
            PartyService service = SystemAPI.ManagedAPI.GetSingleton<PartyServiceComponent>().Value;

            foreach (NetCodeConnectionEvent connectionEvent in SystemAPI.GetSingleton<NetworkStreamDriver>().ConnectionEventsForTick)
            {
                // Id 0 은 Handshake 전에 끊긴 접속. 플레이어가 아님.
                if (connectionEvent.State != ConnectionState.State.Disconnected || connectionEvent.Id.Value == 0)
                {
                    continue;
                }

                // 끊긴 Connection 에는 이름 정리 컴포넌트만 남아 있음. PlayerDisconnectSystem 이 지우기 전에 읽음.
                Entity connection = connectionEvent.ConnectionEntity;
                FixedString64Bytes playerName = state.EntityManager.HasComponent<PlayerNameCleanup>(connection)
                    ? state.EntityManager.GetComponentData<PlayerNameCleanup>(connection).Value
                    : default;

                service.Disconnect(connectionEvent.Id.Value, playerName);
            }

            if (!commandQuery.IsEmpty)
            {
                using NativeArray<ReceiveRpcCommandRequest> requests = commandQuery.ToComponentDataArray<ReceiveRpcCommandRequest>(Allocator.Temp);
                using NativeArray<PartyCommandRPC> rpcs = commandQuery.ToComponentDataArray<PartyCommandRPC>(Allocator.Temp);

                state.EntityManager.DestroyEntity(commandQuery);

                for (int i = 0; i < rpcs.Length; i++)
                {
                    // 입장 전이거나 보낸 뒤 접속이 끊긴 요청은 버림.
                    Entity connection = requests[i].SourceConnection;

                    if (state.EntityManager.HasComponent<PlayerNameComponent>(connection))
                    {
                        service.Execute(state.EntityManager.GetComponentData<NetworkId>(connection).Value, rpcs[i]);
                    }
                }
            }

            service.Update();
        }
    }
}