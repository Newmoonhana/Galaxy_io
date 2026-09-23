using GalaxyIO.DOTS.Components.Actor;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.NetCode;
using UnityEngine;

namespace GalaxyIO.DOTS.Systems
{
    /// <summary>
    /// 접속이 끊긴 플레이어의 우주선을 제거하고 퇴장 로그를 남기는 시스템.
    /// <p/>
    /// <see cref="NetworkId"/>는 접속이 끊기면 다음 접속에 재사용되므로, 치우지 않으면
    /// 새 접속이 남의 기체를 소유한 채로 시작함.
    /// </summary>
    [BurstCompile]
    [WorldSystemFilter(WorldSystemFilterFlags.ServerSimulation)]
    [UpdateInGroup(typeof(DespawnSystemGroup))]
    public partial struct PlayerDisconnectSystem : ISystem
    {
        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            // 우주선이 없어도(전원 사망) 퇴장 로그는 남겨야 하므로 우주선 쿼리는 요구하지 않음.
            state.RequireForUpdate<NetworkStreamDriver>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var driver = SystemAPI.GetSingleton<NetworkStreamDriver>();
            var disconnected = new NativeHashSet<int>(4, Allocator.Temp);
            var ecb = new EntityCommandBuffer(Allocator.Temp);

            foreach (NetCodeConnectionEvent connectionEvent in driver.ConnectionEventsForTick)
            {
                // Id 0 은 Handshake 전에 끊긴 접속. 플레이어가 아님.
                if (connectionEvent.State != ConnectionState.State.Disconnected || connectionEvent.Id.Value == 0)
                {
                    continue;
                }

                disconnected.Add(connectionEvent.Id.Value);

                // 끊긴 Connection 에는 이름 정리 컴포넌트만 남아 있음. 읽은 뒤 제거해야 Entity 가 완전히 사라짐.
                Entity connection = connectionEvent.ConnectionEntity;
                FixedString64Bytes playerName = default;

                if (SystemAPI.HasComponent<PlayerNameCleanup>(connection))
                {
                    playerName = SystemAPI.GetComponent<PlayerNameCleanup>(connection).Value;
                    ecb.RemoveComponent<PlayerNameCleanup>(connection);
                }

                Debug.Log($"[Server] Left: {playerName} (NetworkId {connectionEvent.Id.Value}, {connectionEvent.DisconnectReason.ToFixedString()})");
            }

            // 끊긴 접속이 이번 틱에 없으면 우주선을 훑지 않음.
            if (disconnected.IsEmpty)
            {
                ecb.Dispose();
                disconnected.Dispose();
                return;
            }

            foreach (
                var (owner, entity) in
                SystemAPI.Query<RefRO<GhostOwner>>()
                    .WithAll<SpaceshipSpecComponent>()
                    .WithEntityAccess()
            ) {
                if (disconnected.Contains(owner.ValueRO.NetworkId))
                {
                    ecb.DestroyEntity(entity);
                }
            }

            ecb.Playback(state.EntityManager);
            ecb.Dispose();
            disconnected.Dispose();
        }
    }
}