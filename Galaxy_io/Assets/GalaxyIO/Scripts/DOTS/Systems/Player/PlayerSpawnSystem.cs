using GalaxyIO.DOTS.Components;
using GalaxyIO.DOTS.Components.Config;
using Unity.Collections;
using Unity.Entities;
using Unity.NetCode;

namespace GalaxyIO.DOTS.Systems
{
    [WorldSystemFilter(WorldSystemFilterFlags.ServerSimulation)]
    public partial struct PlayerSpawnSystem : ISystem
    {
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<SpaceshipPresetComponent>();
            state.RequireForUpdate<SpawnRequestRPC>();
        }

        public void OnUpdate(ref SystemState state)
        {
            SpaceshipPresetComponent shipPresets = SystemAPI.GetSingleton<SpaceshipPresetComponent>();

            // Query 순회 도중 Instantiate / DestroyEntity 를 직접 호출하면 순회가 무효화되므로
            // 구조 변경(Structural Change)은 ECB 로 미뤄서 처리한다.
            var ecb = new EntityCommandBuffer(Allocator.Temp);

            foreach (
                var (request, rpcEntity) in
                SystemAPI.Query<RefRO<ReceiveRpcCommandRequest>>()
                    .WithAll<SpawnRequestRPC>()
                    .WithEntityAccess()
            ) {
                Entity connectionEntity = request.ValueRO.SourceConnection;

                // 해당 Connection 이 InGame 상태여야 Ghost 스냅샷이 전송된다.
                ecb.AddComponent<NetworkStreamInGame>(connectionEntity);

                Entity playerShip = ecb.Instantiate(shipPresets.PlayerDefaultShip);

                // 해당 Client에게 Ownership 부여
                // (GhostOwner 는 Ghost Prefab 의 Has Owner 옵션이 켜져 있어야 존재한다)
                var networkId = SystemAPI.GetComponent<NetworkId>(connectionEntity);

                ecb.SetComponent(
                    playerShip,
                    new GhostOwner
                    {
                        NetworkId = networkId.Value
                    }
                );

                // 스폰이 완료되었으니 RPC Entity 제거
                ecb.DestroyEntity(rpcEntity);
            }

            ecb.Playback(state.EntityManager);
            ecb.Dispose();
        }
    }
}