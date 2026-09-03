using GalaxyIO.DOTS.Bootstrap;
using GalaxyIO.DOTS.Client.Components;
using GalaxyIO.DOTS.Components;
using Unity.Collections;
using Unity.Entities;
using Unity.NetCode;

namespace GalaxyIO.DOTS.Client.Systems
{
    /// <summary>
    /// <see cref="PlayerSpawnRequest"/>가 있을 때 서버 접속 -> 스폰 RPC 전송을
    /// 순서대로 처리하는 시스템.
    /// <p/>
    /// 접속을 부트스트랩이 아니라 이 시스템이 담당하는 이유:
    /// Netcode 의 자동 접속은 클라이언트 월드가 만들어질 때 단 한 번만 시도한다.
    /// 서버보다 클라이언트가 먼저 떠 있는 상황을 다루려면 재시도가 필요하다.
    /// <p/>
    /// 또한 Connection Entity 는 Handshake 이전(Connecting 상태)에도 존재하므로,
    /// "Connection 이 있다 == 접속됐다" 가 아니다. Handshake 가 끝나 <see cref="NetworkId"/>가
    /// 붙은 뒤에 RPC 를 보내야 한다.
    /// </summary>
    [WorldSystemFilter(WorldSystemFilterFlags.ClientSimulation)]
    public partial struct ClientConnectionSystem : ISystem
    {
        // 접속 재시도 간격(초).
        private const double RetryInterval = 2.0;

        // 다음 접속 시도가 허용되는 시각.
        private double nextAttemptTime;

        private EntityQuery connectionQuery;
        private EntityQuery requestQuery;

        public void OnCreate(ref SystemState state)
        {
            // 게임 시작을 누르기 전에는 아무 일도 하지 않는다.
            state.RequireForUpdate<PlayerSpawnRequest>();
            state.RequireForUpdate<NetworkStreamDriver>();

            connectionQuery = state.GetEntityQuery(ComponentType.ReadOnly<NetworkStreamConnection>());
            requestQuery = state.GetEntityQuery(ComponentType.ReadOnly<PlayerSpawnRequest>());
        }

        public void OnUpdate(ref SystemState state)
        {
            // 접속이 없는 경우: 최초 시도이거나, 실패해서 Netcode 가 Connection Entity 를
            // 파괴한 경우다. 두 경우 모두 간격을 두고 다시 시도한다.
            if (connectionQuery.IsEmpty)
            {
                double now = SystemAPI.Time.ElapsedTime;

                if (now < nextAttemptTime)
                {
                    return;
                }

                nextAttemptTime = now + RetryInterval;

                SystemAPI.GetSingletonRW<NetworkStreamDriver>()
                    .ValueRW
                    .Connect(state.EntityManager, NetcodeBootstrap.ServerEndpoint);

                return;
            }

            Entity connectionEntity = connectionQuery.GetSingletonEntity();

            // NetworkId 는 Handshake 가 끝나야 붙는다. 그 전에는 기다린다.
            if (!SystemAPI.HasComponent<NetworkId>(connectionEntity))
            {
                return;
            }

            var ecb = new EntityCommandBuffer(Allocator.Temp);

            // 서버가 보내는 Ghost 스냅샷을 받으려면 클라이언트 Connection 도 InGame 이어야 한다.
            if (!SystemAPI.HasComponent<NetworkStreamInGame>(connectionEntity))
            {
                ecb.AddComponent<NetworkStreamInGame>(connectionEntity);
            }

            Entity rpcEntity = ecb.CreateEntity();

            ecb.AddComponent<SpawnRequestRPC>(rpcEntity);

            ecb.AddComponent(
                rpcEntity,
                new SendRpcCommandRequest
                {
                    TargetConnection = connectionEntity
                }
            );

            ecb.Playback(state.EntityManager);
            ecb.Dispose();

            // 요청 처리 완료. 다시 누를 때까지 이 시스템은 멈춘다.
            state.EntityManager.DestroyEntity(requestQuery);
        }
    }
}
