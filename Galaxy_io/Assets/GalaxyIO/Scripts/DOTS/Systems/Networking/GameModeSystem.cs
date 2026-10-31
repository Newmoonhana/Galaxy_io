using GalaxyIO.DOTS.Components.Actor;
using GalaxyIO.DOTS.Components.Message;
using GalaxyIO.DOTS.Components.Singleton;
using GalaxyIO.DOTS.Match;
using Unity.Collections;
using Unity.Entities;
using Unity.NetCode;

namespace GalaxyIO.DOTS.Systems
{
    /// <summary>
    /// 접속마다 서버의 게임 모드를 알리고, 팀 모드면 <see cref="GameModeService"/>와 ECS 를 잇는 시스템.
    /// 받은 격추 메시지와 팀 변경 요청을 서비스에 넘기고 서비스를 틱마다 갱신함.
    /// </summary>
    [WorldSystemFilter(WorldSystemFilterFlags.ServerSimulation)]
    [UpdateInGroup(typeof(SimulationSystemGroup), OrderFirst = true)]
    [UpdateAfter(typeof(GhostSimulationSystemGroup))]
    [UpdateBefore(typeof(PredictedSimulationSystemGroup))]
    [UpdateAfter(typeof(PartySystem))]
    [UpdateAfter(typeof(PlayerSpawnSystem))]
    public partial struct GameModeSystem : ISystem
    {
        private EntityQuery announceQuery;
        private EntityQuery deathQuery;
        private EntityQuery switchQuery;

        public void OnCreate(ref SystemState state)
        {
            announceQuery = SystemAPI.QueryBuilder().WithAll<NetworkId>().WithNone<GameModeAnnouncedTag>().Build();
            deathQuery = SystemAPI.QueryBuilder().WithAll<DeathMessage>().Build();
            switchQuery = SystemAPI.QueryBuilder().WithAll<ReceiveRpcCommandRequest, TeamSwitchRPC>().Build();

            state.RequireForUpdate<NetworkStreamDriver>();
            state.RequireForUpdate<PartyServiceComponent>();
        }

        public void OnUpdate(ref SystemState state)
        {
            // 씬이 다 들어오기 전에는 모드를 모름. 들어온 뒤에 알림.
            if (!SystemAPI.HasSingleton<PrefabRegistryComponent>())
            {
                return;
            }

            SystemAPI.TryGetSingleton(out GameModeComponent mode);
            SystemAPI.ManagedAPI.TryGetSingleton(out GameModeServiceComponent service);

            if (mode.Mode != GameMode.Free && service == null)
            {
                service = new GameModeServiceComponent
                {
                    Value = new GameModeService(state.EntityManager, SystemAPI.ManagedAPI.GetSingleton<PartyServiceComponent>().Value),
                };

                state.EntityManager.AddComponentObject(state.EntityManager.CreateEntity(), service);
            }

            if (!announceQuery.IsEmpty)
            {
                using NativeArray<Entity> connections = announceQuery.ToEntityArray(Allocator.Temp);

                foreach (Entity connection in connections)
                {
                    Entity rpc = state.EntityManager.CreateEntity();
                    state.EntityManager.AddComponentData(rpc, new GameModeRPC
                    {
                        Mode = mode.Mode,
                        Full = service != null && service.Value.IsFull,
                    });
                    state.EntityManager.AddComponentData(rpc, new SendRpcCommandRequest { TargetConnection = connection });
                }

                state.EntityManager.AddComponent<GameModeAnnouncedTag>(announceQuery);
            }

            if (service == null)
            {
                return;
            }

            GameModeService modes = service.Value;

            if (!deathQuery.IsEmpty)
            {
                using NativeArray<DeathMessage> deaths = deathQuery.ToComponentDataArray<DeathMessage>(Allocator.Temp);
                state.EntityManager.DestroyEntity(deathQuery);

                foreach (DeathMessage death in deaths)
                {
                    modes.Death(death);
                }
            }

            if (!switchQuery.IsEmpty)
            {
                using NativeArray<ReceiveRpcCommandRequest> requests = switchQuery.ToComponentDataArray<ReceiveRpcCommandRequest>(Allocator.Temp);
                state.EntityManager.DestroyEntity(switchQuery);

                foreach (ReceiveRpcCommandRequest request in requests)
                {
                    // 보낸 뒤 접속이 끊긴 요청은 버림.
                    if (state.EntityManager.HasComponent<NetworkId>(request.SourceConnection))
                    {
                        modes.Switch(state.EntityManager.GetComponentData<NetworkId>(request.SourceConnection).Value);
                    }
                }
            }

            modes.Update(
                SystemAPI.Time.ElapsedTime,
                SystemAPI.Time.DeltaTime,
                SystemAPI.GetSingleton<NetworkStreamDriver>().ConnectionEventsForTick
            );
        }
    }
}
