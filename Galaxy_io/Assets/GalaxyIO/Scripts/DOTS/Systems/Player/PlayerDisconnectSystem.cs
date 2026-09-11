using GalaxyIO.DOTS.Components;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.NetCode;

namespace GalaxyIO.DOTS.Systems
{
    /// <summary>
    /// 접속이 끊긴 플레이어의 우주선을 제거하는 시스템.
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
            state.RequireForUpdate<NetworkStreamDriver>();
            state.RequireForUpdate(
                SystemAPI.QueryBuilder().WithAll<SpaceshipSpecComponent, GhostOwner>().Build()
            );
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var driver = SystemAPI.GetSingleton<NetworkStreamDriver>();
            var disconnected = new NativeHashSet<int>(4, Allocator.Temp);

            foreach (NetCodeConnectionEvent connectionEvent in driver.ConnectionEventsForTick)
            {
                if (connectionEvent.State == ConnectionState.State.Disconnected)
                {
                    disconnected.Add(connectionEvent.Id.Value);
                }
            }

            // 끊긴 접속이 이번 틱에 없으면 우주선을 훑지 않음.
            if (disconnected.IsEmpty)
            {
                disconnected.Dispose();
                return;
            }

            var ecb = new EntityCommandBuffer(Allocator.Temp);

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
