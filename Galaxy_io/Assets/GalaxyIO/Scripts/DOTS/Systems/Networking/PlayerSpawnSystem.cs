using GalaxyIO.DOTS.Components.Actor;
using GalaxyIO.DOTS.Components.Message;
using GalaxyIO.DOTS.Components.Singleton;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.NetCode;
using Unity.Transforms;
using UnityEngine;
using Random = Unity.Mathematics.Random;

namespace GalaxyIO.DOTS.Systems
{
    /// <summary>
    /// <see cref="SpawnRequestRPC"/>를 받아 플레이어 우주선을 생성하고
    /// 요청한 클라이언트에게 Ownership을 부여하는 시스템.
    /// </summary>
    [WorldSystemFilter(WorldSystemFilterFlags.ServerSimulation)]
    [UpdateInGroup(typeof(SimulationSystemGroup), OrderFirst = true)]
    [UpdateAfter(typeof(GhostSimulationSystemGroup))]
    [UpdateBefore(typeof(PredictedSimulationSystemGroup))]
    public partial struct PlayerSpawnSystem : ISystem
    {
        // 스폰 좌표 범위(월드 유닛). [-SpawnRange, SpawnRange] 사각형 안에서 균등 추출함.
        private const float SpawnRange = 100.0F;

        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<NetworkTime>();
            state.RequireForUpdate<SpaceshipRegistryElement>();
            state.RequireForUpdate<PlayerSpawnConfigComponent>();
            state.RequireForUpdate<SpawnRequestRPC>();
        }

        public void OnUpdate(ref SystemState state)
        {
            Entity shipPrefab = SystemAPI
                .GetSingletonBuffer<SpaceshipRegistryElement>(true)
                .Find(SystemAPI.GetSingleton<PlayerSpawnConfigComponent>().DefaultSpaceship);

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
                var (request, rpc, rpcEntity) in
                SystemAPI.Query<RefRO<ReceiveRpcCommandRequest>, RefRO<SpawnRequestRPC>>()
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

                // 접속 요청은 이름을 싣고, 리스폰 요청은 비어 있음. 비어 있으면 Connection 에 남긴 이름을 씀.
                FixedString64Bytes playerName = rpc.ValueRO.Name;

                if (playerName.IsEmpty && SystemAPI.HasComponent<PlayerNameComponent>(connectionEntity))
                {
                    playerName = SystemAPI.GetComponent<PlayerNameComponent>(connectionEntity).Value;
                }

                // 이름 없는 요청은 버림. 클라이언트가 빈 이름을 막으므로 정상 경로로는 오지 않음.
                if (playerName.IsEmpty)
                {
                    owners.Remove(networkId.Value);
                    ecb.DestroyEntity(rpcEntity);
                    continue;
                }

                // Connection 에 이름이 없으면 첫 스폰 = 입장.
                if (!SystemAPI.HasComponent<PlayerNameComponent>(connectionEntity))
                {
                    Debug.Log($"[Server] Joined: {playerName} (NetworkId {networkId.Value})");
                    ecb.AddComponent(connectionEntity, new PlayerNameCleanup { Value = playerName });
                }

                ecb.AddComponent(connectionEntity, new PlayerNameComponent { Value = playerName });

                Entity playerShip = ecb.Instantiate(shipPrefab);

                ecb.SetComponent(
                    playerShip,
                    new GhostOwner
                    {
                        NetworkId = networkId.Value
                    }
                );

                ecb.SetComponent(playerShip, new PlayerNameComponent { Value = playerName });

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

                // 복제 반경. 강화는 GhostRelevancySystem 이 다음 틱부터 반영함.
                ecb.AddComponent(connectionEntity, new RelevancyRadiusComponent
                {
                    Value = SystemAPI.GetComponent<SpaceshipRadarComponent>(shipPrefab).Range,
                });

                // 스폰이 완료되었으니 RPC Entity 제거
                ecb.DestroyEntity(rpcEntity);
            }

            ecb.Playback(state.EntityManager);
            ecb.Dispose();
            owners.Dispose();
        }
    }
}