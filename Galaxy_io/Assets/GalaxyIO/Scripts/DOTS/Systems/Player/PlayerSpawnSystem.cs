using GalaxyIO.DOTS.Components;
using GalaxyIO.DOTS.Components.Config;
using Unity.Collections;
using Unity.Entities;
using Unity.NetCode;

namespace GalaxyIO.DOTS.Systems
{
    /// <summary>
    /// <see cref="SpawnRequestRPC"/>를 받아 플레이어 우주선을 생성하고
    /// 요청한 클라이언트에게 Ownership을 부여하는 시스템.
    /// </summary>
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

            // Query 순회 중의 구조 변경(Structural Change)은 ECB 로 미뤄서 처리함.
            var ecb = new EntityCommandBuffer(Allocator.Temp);

            foreach (
                var (request, rpcEntity) in
                SystemAPI.Query<RefRO<ReceiveRpcCommandRequest>>()
                    .WithAll<SpawnRequestRPC>()
                    .WithEntityAccess()
            ) {
                Entity connectionEntity = request.ValueRO.SourceConnection;

                // 해당 Connection 이 InGame 상태여야 Ghost 스냅샷이 전송됨.
                ecb.AddComponent<NetworkStreamInGame>(connectionEntity);

                Entity playerShip = ecb.Instantiate(shipPresets.PlayerDefaultShip);

                // 해당 Client에게 Ownership 부여. (Ghost Prefab 의 Has Owner 옵션 필요)
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