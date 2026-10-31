using GalaxyIO.DOTS.Components.Actor;
using GalaxyIO.DOTS.Components.Message;
using GalaxyIO.DOTS.Components.Singleton;
using GalaxyIO.DOTS.Match;
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
    /// 팀 모드에서는 입장 요청에 기체를 만들지 않고, 리스폰 요청은 <see cref="GameModeService"/>가 검증한 기종과 위치로 만듦.
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

            SystemAPI.ManagedAPI.TryGetSingleton(out GameModeServiceComponent modes);

            // 팀 모드 서비스가 아직 없으면 요청을 남겨 두고 다음 틱에 처리함.
            if (modes == null && SystemAPI.HasSingleton<GameModeComponent>())
            {
                return;
            }

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

            // 팀 모드 검증이 RPC Entity 를 만들 수 있으므로 쿼리 순회 대신 배열로 돎.
            using NativeArray<Entity> rpcEntities = SystemAPI.QueryBuilder()
                .WithAll<ReceiveRpcCommandRequest, SpawnRequestRPC>()
                .Build()
                .ToEntityArray(Allocator.Temp);

            foreach (Entity rpcEntity in rpcEntities)
            {
                Entity connectionEntity = SystemAPI.GetComponent<ReceiveRpcCommandRequest>(rpcEntity).SourceConnection;
                SpawnRequestRPC rpc = SystemAPI.GetComponent<SpawnRequestRPC>(rpcEntity);

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
                FixedString64Bytes playerName = rpc.Name;

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

                bool joining = !SystemAPI.HasComponent<PlayerNameComponent>(connectionEntity);

                // 팀 모드 인원이 차면 들여보내지 않음. 클라이언트가 접속 직후 GameModeRPC 로 먼저 거름.
                if (joining && modes != null && modes.Value.IsFull)
                {
                    ecb.AddComponent<NetworkStreamRequestDisconnect>(connectionEntity);
                    ecb.DestroyEntity(rpcEntity);
                    continue;
                }

                // Connection 에 이름이 없으면 첫 스폰 = 입장.
                if (joining)
                {
                    Debug.Log($"[Server] Joined: {playerName} (NetworkId {networkId.Value})");
                    ecb.AddComponent(connectionEntity, new PlayerNameCleanup { Value = playerName });
                }

                ecb.AddComponent(connectionEntity, new PlayerNameComponent { Value = playerName });

                Entity prefab = shipPrefab;

                // 틱과 NetworkId 를 섞어 같은 틱에 들어온 요청끼리도 좌표가 갈림.
                var random = Random.CreateFromIndex(tick ^ ((uint)networkId.Value * 0x9E3779B9u));

                var spawnPosition = new float3(
                    random.NextFloat2(new float2(-SpawnRange), new float2(SpawnRange)),
                    0.0F
                );

                quaternion spawnRotation = quaternion.identity;

                // 팀 모드: 입장 요청은 팀 배정만 받고 리스폰 창에서 기종을 고름. 리스폰은 서비스가 검증함.
                if (modes != null)
                {
                    if (rpc.Spaceship == SpaceshipId.None
                        || !modes.Value.TrySpawn(networkId.Value, rpc.Spaceship, out spawnPosition, out spawnRotation))
                    {
                        owners.Remove(networkId.Value);
                        ecb.DestroyEntity(rpcEntity);
                        continue;
                    }

                    // 검증 중에 구조 변경이 있었을 수 있어 버퍼를 다시 받음.
                    prefab = SystemAPI.GetSingletonBuffer<SpaceshipRegistryElement>(true).Find(rpc.Spaceship);
                }

                Entity playerShip = ecb.Instantiate(prefab);

                ecb.SetComponent(
                    playerShip,
                    new GhostOwner
                    {
                        NetworkId = networkId.Value
                    }
                );

                ecb.SetComponent(playerShip, new PlayerNameComponent { Value = playerName });

                // 크기는 프리팹 기본값(1)을 그대로 씀.
                ecb.SetComponent(playerShip, LocalTransform.FromPositionRotation(spawnPosition, spawnRotation));

                // Ghost 복제 범위의 중심. 이미 있으면 값만 덮어씀. (리스폰)
                ecb.AddComponent(connectionEntity, new GhostConnectionPosition { Position = spawnPosition });

                // 복제 반경. 강화는 GhostRelevancySystem 이 다음 틱부터 반영함.
                ecb.AddComponent(connectionEntity, new RelevancyRadiusComponent
                {
                    Value = SystemAPI.GetComponent<SpaceshipRadarComponent>(prefab).Range,
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
