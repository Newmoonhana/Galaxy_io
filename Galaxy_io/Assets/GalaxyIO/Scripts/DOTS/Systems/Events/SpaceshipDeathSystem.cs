using GalaxyIO.DOTS.Components.Actor;
using GalaxyIO.DOTS.Components.Message;
using GalaxyIO.DOTS.Components.Singleton;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.NetCode;
using Unity.Transforms;

namespace GalaxyIO.DOTS.Systems
{
    /// <summary>
    /// 체력이 0이 된 우주선을 제거하고 파괴 사실을 근처 클라이언트, 소유자, 막타를 친 플레이어에게 알리는 시스템.
    /// 플레이어 격추는 킬 로그로 전체에 알림. 격추에 따른 점수 변경을 요청함.
    /// 팀 모드에서는 플레이어 격추를 <see cref="DeathMessage"/>로 남김. 위성은 포탑만 꺼지므로 지우지 않음.
    /// </summary>
    [BurstCompile]
    [WorldSystemFilter(WorldSystemFilterFlags.ServerSimulation)]
    [UpdateInGroup(typeof(EventSystemGroup))]
    [UpdateAfter(typeof(DamageNotifySystem))]
    [UpdateAfter(typeof(SpaceshipCrashNotifySystem))]
    public partial struct SpaceshipDeathSystem : ISystem
    {
        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<RelevancyConfigComponent>();
            state.RequireForUpdate(
                SystemAPI.QueryBuilder()
                    .WithAll<SpaceshipSpecComponent, LocalTransform, GhostOwner>()
                    .Build()
            );
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            // 복제 범위 안의 접속과 소유자에게만 보냄.
            RelevancyConfigComponent config = SystemAPI.GetSingleton<RelevancyConfigComponent>();

            var focuses = new NativeList<RelevancyFocus>(4, Allocator.Temp);

            foreach (
                var (position, radius, networkId, connection) in
                SystemAPI.Query<RefRO<GhostConnectionPosition>, RefRO<RelevancyRadiusComponent>, RefRO<NetworkId>>()
                    .WithAll<NetworkStreamInGame>()
                    .WithEntityAccess()
            ) {
                focuses.Add(new RelevancyFocus
                {
                    Connection = connection,
                    NetworkId  = networkId.ValueRO.Value,
                    Position   = position.ValueRO.Position.xy,

                    // 바깥 반경
                    Radius = radius.ValueRO.Value + config.Hysteresis,
                });
            }

            // 킬 로그용 이름. 우주선은 파괴돼도 Connection 에는 이름이 남아 있음.
            var names = new NativeHashMap<int, FixedString64Bytes>(4, Allocator.Temp);

            foreach (var (networkId, playerName) in SystemAPI.Query<RefRO<NetworkId>, RefRO<PlayerNameComponent>>())
            {
                names[networkId.ValueRO.Value] = playerName.ValueRO.Value;
            }

            double time = SystemAPI.Time.ElapsedTime;
            bool teamMode = SystemAPI.HasSingleton<GameModeComponent>();

            // Query 순회 중의 구조 변경(Structural Change)은 ECB 로 미뤄서 처리함.
            var ecb = new EntityCommandBuffer(Allocator.Temp);

            foreach (
                var (spec, transform, owner, entity) in
                SystemAPI.Query<RefRO<SpaceshipSpecComponent>, RefRO<LocalTransform>, RefRO<GhostOwner>>()
                    .WithNone<SatelliteComponent>()
                    .WithEntityAccess()
            ) {
                if (spec.ValueRO.Health > 0.0F)
                {
                    continue;
                }

                var message = new SpaceshipDestroyedRPC
                {
                    Position        = transform.ValueRO.Position.xy,
                    OwnerNetworkId  = owner.ValueRO.NetworkId,
                    KillerNetworkId = SystemAPI.HasComponent<KillingBlowComponent>(entity)
                        ? SystemAPI.GetComponent<KillingBlowComponent>(entity).NetworkId
                        : 0,
                };

                // 막타를 친 플레이어는 멀어도 받음.
                for (int i = 0; i < focuses.Length; i++)
                {
                    if (focuses[i].NetworkId == message.OwnerNetworkId
                        || focuses[i].NetworkId == message.KillerNetworkId
                        || math.distancesq(focuses[i].Position, message.Position) <= focuses[i].Radius * focuses[i].Radius)
                    {
                        Send(ecb, message, focuses[i].Connection);
                    }
                }

                bool isPlayer = SystemAPI.HasComponent<PlayerNameComponent>(entity);

                if (SystemAPI.HasComponent<KillingBlowComponent>(entity))
                {
                    int killer = SystemAPI.GetComponent<KillingBlowComponent>(entity).NetworkId;

                    // AI 막타(-1)와 알 수 없는 막타(0)는 점수 없음. 점수는 격추된 기체의 KillPoint.
                    if (killer > 0 && killer != owner.ValueRO.NetworkId)
                    {
                        Post(ecb, ScoreMessage.Add(killer, spec.ValueRO.KillPoint));
                    }
                }

                // 킬 로그는 플레이어 격추만. Entity.Null 이면 모든 접속에 전송.
                if (isPlayer)
                {
                    KillLogRPC log = MakeKillLog(
                        SystemAPI.GetComponent<KillingBlowComponent>(entity),
                        SystemAPI.GetBuffer<DamageRecordElement>(entity),
                        time, names
                    );

                    log.Victim = SystemAPI.GetComponent<PlayerNameComponent>(entity).Value;
                    log.VictimNetworkId = owner.ValueRO.NetworkId;

                    Send(ecb, log, Entity.Null);

                    if (teamMode && owner.ValueRO.NetworkId > 0)
                    {
                        ecb.AddComponent(ecb.CreateEntity(), new DeathMessage
                        {
                            Victim     = owner.ValueRO.NetworkId,
                            Killer     = log.KillerNetworkId,
                            KillerName = log.Killer,
                            Assistant  = log.AssistantNetworkId,
                            Spaceship  = SystemAPI.GetComponent<SpaceshipKindComponent>(entity).Value,
                        });
                    }

                    // 사망하면 점수를 모두 잃음.
                    Post(ecb, ScoreMessage.Set(owner.ValueRO.NetworkId, 0));
                }

                ecb.DestroyEntity(entity);
            }

            ecb.Playback(state.EntityManager);
            ecb.Dispose();
            names.Dispose();
            focuses.Dispose();
        }

        private static void Send<T>(EntityCommandBuffer ecb, in T message, Entity target)
            where T : unmanaged, IRpcCommand
        {
            Entity rpcEntity = ecb.CreateEntity();

            ecb.AddComponent(rpcEntity, message);
            ecb.AddComponent(rpcEntity, new SendRpcCommandRequest { TargetConnection = target });
        }

        private static void Post(EntityCommandBuffer ecb, in ScoreMessage message)
        {
            ecb.AddComponent(ecb.CreateEntity(), message);
        }

        /// <summary>
        /// 막타와 어시스트를 채움. 격추된 쪽은 호출한 곳이 채움.
        /// 막타 주인을 알 수 없으면(쏜 AI 파괴, 접속 종료) 막타 이름을 비움.
        /// </summary>
        private static KillLogRPC MakeKillLog(
            in KillingBlowComponent blow,
            in DynamicBuffer<DamageRecordElement> records,
            double time,
            in NativeHashMap<int, FixedString64Bytes> names)
        {
            var log = new KillLogRPC { Type = blow.Type };

            if (names.TryGetValue(blow.NetworkId, out FixedString64Bytes killerName))
            {
                log.Killer = killerName;
                log.KillerNetworkId = blow.NetworkId;
            }
            else if (!blow.EnemyName.IsEmpty)
            {
                log.Killer = new FixedString64Bytes(blow.EnemyName);
            }

            log.AssistantNetworkId = FindAssistant(records, time, names, log.KillerNetworkId);

            if (log.AssistantNetworkId != 0)
            {
                log.Assistant = names[log.AssistantNetworkId];
            }

            return log;
        }

        /// <summary>
        /// 막타를 제외하고 누적 데미지가 가장 큰 플레이어. 만료됐거나 접속을 끊은 플레이어는 제외. 없으면 0.
        /// </summary>
        private static int FindAssistant(
            in DynamicBuffer<DamageRecordElement> records,
            double time,
            in NativeHashMap<int, FixedString64Bytes> names,
            int killer)
        {
            int assistant = 0;
            float mostAmount = 0.0F;

            for (int i = 0; i < records.Length; i++)
            {
                DamageRecordElement record = records[i];

                if (record.NetworkId != killer
                    && DamageRecord.IsAlive(record, time)
                    && names.ContainsKey(record.NetworkId)
                    && record.Amount > mostAmount)
                {
                    assistant = record.NetworkId;
                    mostAmount = record.Amount;
                }
            }

            return assistant;
        }
    }
}
