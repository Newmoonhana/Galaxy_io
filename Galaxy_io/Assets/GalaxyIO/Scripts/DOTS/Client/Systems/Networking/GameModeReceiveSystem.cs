using GalaxyIO.DOTS.Client.Components.Singleton;
using GalaxyIO.DOTS.Components.Message;
using GalaxyIO.DOTS.Components.Singleton;
using Unity.Collections;
using Unity.Entities;
using Unity.NetCode;

namespace GalaxyIO.DOTS.Client.Systems
{
    /// <summary>
    /// <see cref="GameModeStateComponent"/> 싱글톤을 만들고, 서버가 보낸 게임 모드, 리스폰, 팀 기체 수, 팀 점수 RPC 를 반영하는 시스템.
    /// 리스폰 알림을 받으면 리스폰 창을 띄움.
    /// </summary>
    [WorldSystemFilter(WorldSystemFilterFlags.ClientSimulation)]
    public partial struct GameModeReceiveSystem : ISystem
    {
        // SpaceshipCategory 개수
        private const int CategoryCount = 4;

        public void OnCreate(ref SystemState state)
        {
            var initial = new GameModeStateComponent();

            for (int i = 0; i < CategoryCount; i++)
            {
                initial.Occupied.Add(0);
                initial.Max.Add(0);
            }

            state.EntityManager.CreateSingleton(initial);

            state.RequireForUpdate<PlayerStateComponent>();
            state.RequireAnyForUpdate(
                SystemAPI.QueryBuilder().WithAll<GameModeRPC, ReceiveRpcCommandRequest>().Build(),
                SystemAPI.QueryBuilder().WithAll<RespawnRPC, ReceiveRpcCommandRequest>().Build(),
                SystemAPI.QueryBuilder().WithAll<TeamSlotRPC, ReceiveRpcCommandRequest>().Build(),
                SystemAPI.QueryBuilder().WithAll<TeamScoreRPC, ReceiveRpcCommandRequest>().Build()
            );
        }

        public void OnUpdate(ref SystemState state)
        {
            RefRW<GameModeStateComponent> mode = SystemAPI.GetSingletonRW<GameModeStateComponent>();
            double time = SystemAPI.Time.ElapsedTime;

            var ecb = new EntityCommandBuffer(Allocator.Temp);

            foreach (var (rpc, entity) in SystemAPI.Query<RefRO<GameModeRPC>>().WithAll<ReceiveRpcCommandRequest>().WithEntityAccess())
            {
                mode.ValueRW.ModeReceived = true;
                mode.ValueRW.Mode = rpc.ValueRO.Mode;
                mode.ValueRW.Full = rpc.ValueRO.Full;
                ecb.DestroyEntity(entity);
            }

            foreach (var (rpc, entity) in SystemAPI.Query<RefRO<RespawnRPC>>().WithAll<ReceiveRpcCommandRequest>().WithEntityAccess())
            {
                mode.ValueRW.Team = rpc.ValueRO.Team;
                mode.ValueRW.RespawnAt = time + rpc.ValueRO.Delay;
                mode.ValueRW.Killer = rpc.ValueRO.Killer;

                // 리스폰 창이 이 값을 보고 열림.
                SystemAPI.GetSingletonRW<PlayerStateComponent>().ValueRW.Dead = true;
                ecb.DestroyEntity(entity);
            }

            foreach (var (rpc, entity) in SystemAPI.Query<RefRO<TeamSlotRPC>>().WithAll<ReceiveRpcCommandRequest>().WithEntityAccess())
            {
                int category = (int)rpc.ValueRO.Category;

                if (category < CategoryCount)
                {
                    mode.ValueRW.Occupied[category] = rpc.ValueRO.Occupied;
                    mode.ValueRW.Max[category] = rpc.ValueRO.Max;
                }

                ecb.DestroyEntity(entity);
            }

            foreach (var (rpc, entity) in SystemAPI.Query<RefRO<TeamScoreRPC>>().WithAll<ReceiveRpcCommandRequest>().WithEntityAccess())
            {
                mode.ValueRW.RedScore = rpc.ValueRO.Red;
                mode.ValueRW.BlueScore = rpc.ValueRO.Blue;
                mode.ValueRW.Winner = rpc.ValueRO.Winner;

                double now = SystemAPI.Time.ElapsedTime;

                if (rpc.ValueRO.Winner != Team.None)
                {
                    mode.ValueRW.RestartAt = now + rpc.ValueRO.RestartIn;
                }

                // 새 판. 서버가 맵을 다시 만들었으니 새 위성, 대장선을 받고 지난 판 것이 사라질 때까지 가림.
                if (rpc.ValueRO.NewRound)
                {
                    mode.ValueRW.LoadingUntil = now + GameModeStateComponent.RebuildLoadingTime;
                }

                ecb.DestroyEntity(entity);
            }

            ecb.Playback(state.EntityManager);
            ecb.Dispose();
        }
    }
}
