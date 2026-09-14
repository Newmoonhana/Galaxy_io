using GalaxyIO.DOTS.Client.Components.Message;
using GalaxyIO.DOTS.Components.Message;
using Unity.Collections;
using Unity.Entities;
using Unity.NetCode;

namespace GalaxyIO.DOTS.Client.Systems
{
    /// <summary>
    /// <see cref="PlayerSpawnRequest"/>가 있을 때 서버 접속 -> 스폰 RPC 전송을
    /// 순서대로 처리하는 시스템. 접속 요청 시점을 제어하도록 도와줌.
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
            // 게임 시작을 누르기 전에는 동작하지 않음.
            state.RequireForUpdate<PlayerSpawnRequest>();
            state.RequireForUpdate<NetworkStreamDriver>();

            connectionQuery = state.GetEntityQuery(ComponentType.ReadOnly<NetworkStreamConnection>());
            requestQuery = state.GetEntityQuery(ComponentType.ReadOnly<PlayerSpawnRequest>());
        }

        public void OnUpdate(ref SystemState state)
        {
            PlayerSpawnRequest request = requestQuery.GetSingleton<PlayerSpawnRequest>();

            // 접속이 없는 경우: 최초 시도이거나 접속에 실패한 경우.
            // 두 경우 모두 간격을 두고 재시도함.
            if (connectionQuery.IsEmpty)
            {
                // 리스폰 요청에는 주소가 없음. 접속이 끊긴 상태라면 처리할 수 없으니 버림.
                if (!request.Endpoint.IsValid)
                {
                    state.EntityManager.DestroyEntity(requestQuery);
                    return;
                }

                double now = SystemAPI.Time.ElapsedTime;

                if (now < nextAttemptTime)
                {
                    return;
                }

                nextAttemptTime = now + RetryInterval;

                SystemAPI.GetSingletonRW<NetworkStreamDriver>()
                    .ValueRW
                    .Connect(state.EntityManager, request.Endpoint);

                return;
            }

            Entity connectionEntity = connectionQuery.GetSingletonEntity();

            // Handshake 가 끝나야 NetworkId 가 붙음. 그 전에는 대기.
            if (!SystemAPI.HasComponent<NetworkId>(connectionEntity))
            {
                return;
            }

            var ecb = new EntityCommandBuffer(Allocator.Temp);

            // Ghost 스냅샷을 받으려면 클라이언트 Connection 도 InGame 이어야 함.
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

            // 요청 처리 완료. 다시 누를 때까지 정지함.
            state.EntityManager.DestroyEntity(requestQuery);
        }
    }
}