using GalaxyIO.DOTS.Client.Components.Message;
using GalaxyIO.DOTS.Client.Components.Singleton;
using GalaxyIO.DOTS.Components.Message;
using GalaxyIO.DOTS.Components.Singleton;
using Unity.Collections;
using Unity.Entities;
using Unity.NetCode;
using Unity.Networking.Transport;
using UnityEngine;

namespace GalaxyIO.DOTS.Client.Networking
{
    /// <summary>
    /// 클라이언트에서 서버로 보내는 요청들을 모아놓은 Helper 클래스.
    /// </summary>
    public static class ClientNetworkRequestManager
    {
        /// <summary>
        /// 지정한 주소의 서버에 접속해 플레이어 스폰을 요청함. 의사 표시만 남기고,
        /// 실제 접속과 RPC 전송은 <see cref="Systems.ClientConnectionSystem"/>이 처리함.
        /// (DOTS Entities가 제공하는 Networking 기능을 이용하기 위해)
        /// </summary>
        /// <param name="playerName">플레이어 이름. 비어 있으면 서버가 요청을 버림.</param>
        /// <returns>
        /// 요청 접수 여부. Client World가 없으면 false. 접수됐다는 뜻이지
        /// 접속 완료를 의미하지는 않음.
        /// </returns>
        public static bool RequestPlayerSpawn(NetworkEndpoint endpoint, string playerName)
        {
            var name = new FixedString64Bytes();
            name.CopyFromTruncated(playerName);

            return Enqueue(endpoint, name, SpaceshipId.None);
        }

        /// <summary>
        /// 파괴된 뒤 다시 스폰을 요청함. 이미 접속돼 있으므로 주소를 싣지 않음.
        /// </summary>
        /// <param name="spaceship">팀 모드에서 탈 기종. 그 밖에는 None.</param>
        public static bool RequestRespawn(SpaceshipId spaceship = SpaceshipId.None)
        {
            var clientWorld = ClientServerBootstrap.ClientWorld;

            if (clientWorld == null || !Enqueue(default, default, spaceship))
            {
                return false;
            }

            // Dead 가 꺼지면 리스폰 창이 닫힘.
            using var stateQuery = clientWorld.EntityManager.CreateEntityQuery(
                ComponentType.ReadWrite<PlayerStateComponent>()
            );

            stateQuery.GetSingletonRW<PlayerStateComponent>().ValueRW.Dead = false;

            return true;
        }

        /// <summary>
        /// 서버 접속을 끊음. 서버는 접속 종료를 보고 우주선을 제거함.
        /// </summary>
        /// <returns>요청 접수 여부. Client World가 없으면 false.</returns>
        public static bool RequestQuit()
        {
            var clientWorld = ClientServerBootstrap.ClientWorld;

            if (clientWorld == null)
            {
                Debug.LogWarning("Client World not found.");
                return false;
            }

            var entityManager = clientWorld.EntityManager;

            using var connectionQuery = entityManager.CreateEntityQuery(
                ComponentType.ReadOnly<NetworkStreamConnection>()
            );
            using var requestQuery = entityManager.CreateEntityQuery(
                ComponentType.ReadOnly<PlayerSpawnRequest>()
            );
            using var stateQuery = entityManager.CreateEntityQuery(
                ComponentType.ReadWrite<PlayerStateComponent>()
            );

            // 대기 중인 리스폰 요청 취소. Dead 가 꺼지면 리스폰 창이 닫힘.
            entityManager.DestroyEntity(requestQuery);
            stateQuery.GetSingletonRW<PlayerStateComponent>().ValueRW.Dead = false;

            entityManager.AddComponent<NetworkStreamRequestDisconnect>(connectionQuery);

            return true;
        }

        /// <summary>
        /// 월드 업데이트 없이 즉시 접속을 끊고 끊김 패킷을 보냄. 앱 종료 시 사용.
        /// </summary>
        public static void DisconnectImmediately()
        {
            var clientWorld = ClientServerBootstrap.ClientWorld;

            if (clientWorld == null || !clientWorld.IsCreated)
            {
                return;
            }

            var entityManager = clientWorld.EntityManager;

            using var driverQuery = entityManager.CreateEntityQuery(
                ComponentType.ReadWrite<NetworkStreamDriver>()
            );
            using var connectionQuery = entityManager.CreateEntityQuery(
                ComponentType.ReadOnly<NetworkStreamConnection>()
            );

            if (driverQuery.IsEmpty || connectionQuery.IsEmpty)
            {
                return;
            }

            // 드라이버를 쓰는 잡이 모두 끝난 뒤에 접근.
            entityManager.CompleteAllTrackedJobs();

            ref NetworkStreamDriver streamDriver = ref driverQuery.GetSingletonRW<NetworkStreamDriver>().ValueRW;
            ref NetworkDriverStore driverStore = ref streamDriver.DriverStore;

            using var connections = connectionQuery.ToComponentDataArray<NetworkStreamConnection>(Allocator.Temp);

            foreach (NetworkStreamConnection connection in connections)
            {
                streamDriver.Disconnect(connection);
            }

            // 끊김 패킷은 드라이버 업데이트에서 전송됨.
            for (int i = driverStore.FirstDriver; i < driverStore.LastDriver; i++)
            {
                ref NetworkDriver driver = ref driverStore.GetDriverRW(i);

                driver.ScheduleUpdate().Complete();
                driver.ScheduleFlushSend(default).Complete();
            }
        }

        /// <summary>
        /// 상대 팀으로 옮겨 달라고 요청함. 팀 모드에서만 받음.
        /// </summary>
        public static void RequestTeamSwitch()
        {
            var clientWorld = ClientServerBootstrap.ClientWorld;

            if (clientWorld == null)
            {
                return;
            }

            var entityManager = clientWorld.EntityManager;

            using var connectionQuery = entityManager.CreateEntityQuery(ComponentType.ReadOnly<NetworkId>());

            if (connectionQuery.IsEmpty)
            {
                return;
            }

            Entity rpc = entityManager.CreateEntity();
            entityManager.AddComponentData(rpc, new TeamSwitchRPC());
            entityManager.AddComponentData(rpc, new SendRpcCommandRequest { TargetConnection = connectionQuery.GetSingletonEntity() });
        }

        /// <summary>
        /// 요청 엔티티를 남김.
        /// </summary>
        private static bool Enqueue(NetworkEndpoint endpoint, FixedString64Bytes name, SpaceshipId spaceship)
        {
            // Thin Client 를 제외한 실제 Client World.
            var clientWorld = ClientServerBootstrap.ClientWorld;

            if (clientWorld == null)
            {
                Debug.LogWarning("Client World not found.");
                return false;
            }

            var entityManager = clientWorld.EntityManager;

            using var requestQuery = entityManager.CreateEntityQuery(
                ComponentType.ReadOnly<PlayerSpawnRequest>()
            );

            // 대기 중인 요청이 있으면 중복 생성하지 않음.
            if (requestQuery.IsEmpty)
            {
                Entity request = entityManager.CreateEntity();

                entityManager.AddComponentData(request, new PlayerSpawnRequest
                {
                    Endpoint  = endpoint,
                    Name      = name,
                    Spaceship = spaceship,
                });
            }

            return true;
        }
    }
}