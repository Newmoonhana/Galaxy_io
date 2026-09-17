using GalaxyIO.DOTS.Components.Actor;
using GalaxyIO.DOTS.Components.Message;
using GalaxyIO.DOTS.Components.Singleton;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.NetCode;
using Unity.Transforms;
using Random = Unity.Mathematics.Random;

namespace GalaxyIO.DOTS.Systems
{
    /// <summary>
    /// <see cref="SpawnRequestRPC"/>를 받아 플레이어 우주선을 생성하고
    /// 요청한 클라이언트에게 Ownership을 부여하는 시스템.
    /// </summary>
    [WorldSystemFilter(WorldSystemFilterFlags.ServerSimulation)]
    public partial struct PlayerSpawnSystem : ISystem
    {
        // 스폰 좌표 범위(월드 유닛). [-SpawnRange, SpawnRange] 사각형 안에서 균등 추출함.
        private const float SpawnRange = 5.0F;

        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<NetworkTime>();
            state.RequireForUpdate<PrefabRegistryElement>();
            state.RequireForUpdate<SpawnRequestRPC>();
        }

        public void OnUpdate(ref SystemState state)
        {
            Entity shipPrefab = SystemAPI
                .GetSingletonBuffer<PrefabRegistryElement>(true)
                .Find(PrefabId.PlayerDefaultShip);

            // 레지스트리에 기체가 없으면 베이킹 설정 문제. 스폰할 것이 없음.
            if (shipPrefab == Entity.Null)
            {
                return;
            }

            uint tick = SystemAPI.GetSingleton<NetworkTime>().ServerTick.SerializedData;

            // 이미 우주선을 가진 소유자 목록. 중복 요청으로 기체가 늘어나는 것 방지.
            var owners = new NativeHashSet<int>(8, Allocator.Temp);

            foreach (var owner in SystemAPI.Query<RefRO<GhostOwner>>().WithAll<SpaceshipSpecComponent>())
            {
                owners.Add(owner.ValueRO.NetworkId);
            }

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

                // 해당 Client에게 Ownership 부여. (Ghost Prefab 의 Has Owner 옵션 필요)
                var networkId = SystemAPI.GetComponent<NetworkId>(connectionEntity);

                // 살아 있는 우주선이 이미 있으면 요청을 버림.
                if (!owners.Add(networkId.Value))
                {
                    ecb.DestroyEntity(rpcEntity);
                    continue;
                }

                Entity playerShip = ecb.Instantiate(shipPrefab);

                ecb.SetComponent(
                    playerShip,
                    new GhostOwner
                    {
                        NetworkId = networkId.Value
                    }
                );

                // 틱과 NetworkId 를 섞어 같은 틱에 들어온 요청끼리도 좌표가 갈림.
                var random = Random.CreateFromIndex(tick ^ ((uint)networkId.Value * 0x9E3779B9u));

                var spawnPosition = new float3(
                    random.NextFloat2(new float2(-SpawnRange), new float2(SpawnRange)),
                    0.0F
                );

                // 회전과 크기는 프리팹 기본값(identity, 1)을 그대로 씀.
                ecb.SetComponent(playerShip, LocalTransform.FromPosition(spawnPosition));

                // Ghost 복제 범위의 중심. 이미 있으면 값만 덮어씀. (리스폰)
                ecb.AddComponent(connectionEntity, new GhostConnectionPosition { Position = spawnPosition });

                // 스폰이 완료되었으니 RPC Entity 제거
                ecb.DestroyEntity(rpcEntity);
            }

            ecb.Playback(state.EntityManager);
            ecb.Dispose();
            owners.Dispose();
        }
    }
}