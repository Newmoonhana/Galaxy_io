using System.Collections.Generic;
using GalaxyIO.DOTS.Components.Actor;
using GalaxyIO.DOTS.Components.Message;
using GalaxyIO.DOTS.Components.Singleton;
using GalaxyIO.DOTS.Party;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.NetCode;
using Unity.Transforms;
using Random = Unity.Mathematics.Random;

namespace GalaxyIO.DOTS.Match
{
    /// <summary>
    /// 팀 모드(대장선 잡기, 점령전)의 서버 규칙. 서버 월드당 한 개. 모드 씬에서만 만들어짐.
    /// 팀 배정, 처치 기록, 부활 대기와 기체 수 제한, 전투 구역, 판의 시작과 승패를 다룸.
    /// </summary>
    public sealed class GameModeService
    {
        private sealed class Player
        {
            public Team Team;
            public FixedString64Bytes Name;

            // 타고 있거나 마지막으로 탄 기종
            public SpaceshipId Spaceship;

            public int Kills;
            public int Assists;
            public int Deaths;

            // 마지막으로 보낸 응답 시간(ms)
            public int Ping;

            // 이 시각(ElapsedTime)부터 부활할 수 있음.
            public double RespawnAt;

            // 전투 구역 밖에 있던 시간(초)
            public float OutOfZone;

            // 점수판에 다시 보내야 함.
            public bool Dirty;
        }

        // 격추된 기체가 자리를 비우기 전까지 차지하는 자리
        private struct Wreck
        {
            public Team Team;
            public SpaceshipCategory Category;
            public double FreeAt;
        }

        // 응답 시간을 다시 보내는 간격(초)과 다시 보낼 만큼의 차이(ms)
        private const double PingInterval = 2.0;
        private const int PingThreshold = 5;

        // 대장선 근처 부활 거리. 선체 바깥부터 (최소, 최대)
        private static readonly float2 KingSpawnGap = new float2(20.0F, 60.0F);

        // 함재기가 찾은 적 대장선 핑을 다시 보내는 간격(초)
        private const double CraftPingInterval = 1.0;

        private readonly EntityManager entityManager;
        private readonly PartyService party;

        private readonly EntityQuery modeQuery;
        private readonly EntityQuery registryQuery;
        private readonly EntityQuery connectionQuery;
        private readonly EntityQuery shipQuery;
        private readonly EntityQuery kingQuery;
        private readonly EntityQuery satelliteQuery;
        private readonly EntityQuery kingConfigQuery;
        private readonly EntityQuery conquestQuery;
        private readonly EntityQuery craftQuery;

        // 새 판에 맵을 다시 만들 때 지우는 것. AI 적, 모든 함재기, 떨어진 아이템, 날아가는 투사체.
        private readonly EntityQuery enemyQuery;
        private readonly EntityQuery allCraftQuery;
        private readonly EntityQuery itemQuery;
        private readonly EntityQuery projectileQuery;
        private readonly EntityQuery sharedProjectileQuery;

        // NetworkId -> 플레이어
        private readonly Dictionary<int, Player> players = new Dictionary<int, Player>();
        private readonly List<Wreck> wrecks = new List<Wreck>();

        // (팀, 분류) -> 마지막으로 보낸 기체 수
        private readonly Dictionary<(Team, SpaceshipCategory), int> sentSlots = new Dictionary<(Team, SpaceshipCategory), int>();

        // 팀 -> 함재기가 찾은 적 대장선 핑을 마지막으로 보낸 시각. 핑이 없으면 키가 없음.
        private readonly Dictionary<Team, double> craftPings = new Dictionary<Team, double>();

        // 위성 하나의 점령 범위에 있는 플레이어. 기체나 함재기가 있으면 한 명으로 셈.
        private readonly HashSet<int> occupants = new HashSet<int>();

        private Random random = Random.CreateFromIndex((uint)System.Environment.TickCount & 0x7FFFFFFFu);

        private int redScore;
        private int blueScore;
        private Team winner;
        private bool roundStarted;
        private double restartAt;
        private double nextPingTime;
        private double time;

        public GameModeService(EntityManager entityManager, PartyService party)
        {
            this.entityManager = entityManager;
            this.party = party;

            modeQuery = entityManager.CreateEntityQuery(
                ComponentType.ReadOnly<GameModeComponent>(),
                ComponentType.ReadOnly<ShipLimitElement>()
            );
            registryQuery = entityManager.CreateEntityQuery(ComponentType.ReadOnly<SpaceshipRegistryElement>());

            // 입장한 플레이어의 Connection
            connectionQuery = entityManager.CreateEntityQuery(
                ComponentType.ReadOnly<NetworkId>(),
                ComponentType.ReadOnly<PlayerNameComponent>(),
                ComponentType.ReadOnly<NetworkStreamInGame>()
            );
            shipQuery = entityManager.CreateEntityQuery(
                ComponentType.ReadOnly<GhostOwner>(),
                ComponentType.ReadOnly<PlayerNameComponent>(),
                ComponentType.ReadOnly<SpaceshipKindComponent>(),
                ComponentType.ReadOnly<LocalTransform>(),
                ComponentType.ReadWrite<SpaceshipSpecComponent>()
            );
            kingQuery = entityManager.CreateEntityQuery(
                ComponentType.ReadOnly<KingCarrierComponent>(),
                ComponentType.ReadOnly<LocalTransform>()
            );
            satelliteQuery = entityManager.CreateEntityQuery(ComponentType.ReadWrite<SatelliteComponent>());
            kingConfigQuery = entityManager.CreateEntityQuery(ComponentType.ReadOnly<KingCarrierConfigComponent>());
            conquestQuery = entityManager.CreateEntityQuery(
                ComponentType.ReadOnly<ConquestConfigComponent>(),
                ComponentType.ReadOnly<SatelliteSpawnElement>()
            );
            craftQuery = entityManager.CreateEntityQuery(
                ComponentType.ReadOnly<CarriedCraftComponent>(),
                ComponentType.ReadOnly<SpaceshipSpecComponent>(),
                ComponentType.ReadOnly<LocalTransform>()
            );
            enemyQuery = entityManager.CreateEntityQuery(new EntityQueryDesc
            {
                All = new[] { ComponentType.ReadOnly<EnemyPerceptionComponent>() },
                None = new[] { ComponentType.ReadOnly<CarriedCraftComponent>() },
            });
            allCraftQuery = entityManager.CreateEntityQuery(ComponentType.ReadOnly<CarriedCraftComponent>());
            itemQuery = entityManager.CreateEntityQuery(ComponentType.ReadOnly<ItemComponent>());
            projectileQuery = entityManager.CreateEntityQuery(ComponentType.ReadOnly<StraightforwardProjectileComponent>());
            sharedProjectileQuery = entityManager.CreateEntityQuery(ComponentType.ReadOnly<SharedProjectileComponent>());
        }

        private GameModeComponent Mode => modeQuery.GetSingleton<GameModeComponent>();

        /// <summary>
        /// 최대 인원이 찼는지.
        /// </summary>
        public bool IsFull => players.Count >= Mode.MaxPlayers;

        public Team TeamOf(int networkId)
        {
            return players.TryGetValue(networkId, out Player player) ? player.Team : Team.None;
        }

        /// <summary>
        /// 서버 틱마다 호출. 입장, 퇴장, 판 진행, 위성, 기체 수와 점수판 전송을 처리함.
        /// </summary>
        public void Update(double elapsedTime, float deltaTime, NativeArray<NetCodeConnectionEvent>.ReadOnly connectionEvents)
        {
            time = elapsedTime;

            foreach (NetCodeConnectionEvent connectionEvent in connectionEvents)
            {
                if (connectionEvent.State == ConnectionState.State.Disconnected && players.Remove(connectionEvent.Id.Value))
                {
                    Send(new PlayerStatRPC { NetworkId = connectionEvent.Id.Value, Left = true }, Entity.Null);
                }
            }

            Join();
            UpdateShips(deltaTime);

            if (!roundStarted || (winner != Team.None && time >= restartAt))
            {
                StartRound();
            }
            else if (winner == Team.None)
            {
                UpdateSatellites(deltaTime);
                CheckWinner();
            }

            wrecks.RemoveAll(wreck => time >= wreck.FreeAt);

            SendSlots();
            SendStats();
        }

        /// <summary>
        /// 플레이어 격추. 처치 기록을 남기고 부활 대기를 시작함.
        /// </summary>
        public void Death(in DeathMessage message)
        {
            if (!players.TryGetValue(message.Victim, out Player victim))
            {
                return;
            }

            victim.Deaths++;
            victim.Dirty = true;

            if (message.Killer > 0 && message.Killer != message.Victim && players.TryGetValue(message.Killer, out Player killer))
            {
                killer.Kills++;
                killer.Dirty = true;
            }

            if (message.Assistant > 0 && players.TryGetValue(message.Assistant, out Player assistant))
            {
                assistant.Assists++;
                assistant.Dirty = true;
            }

            float delay = 0.0F;

            if (TryGetLimit(message.Spaceship, out ShipLimitElement limit))
            {
                delay = limit.PlayerDelay;

                if (limit.ShipDelay > 0.0F)
                {
                    wrecks.Add(new Wreck { Team = victim.Team, Category = limit.Category, FreeAt = time + limit.ShipDelay });
                }
            }

            victim.RespawnAt = time + delay;
            SendRespawn(message.Victim, victim, message.KillerName);
        }

        /// <summary>
        /// 상대 팀으로 옮김. 옮긴 뒤 두 팀 인원 차이가 3 이하일 때만 옮김. 타고 있던 기체는 바로 사라지고 기다림 없이 부활함.
        /// </summary>
        public void Switch(int networkId)
        {
            if (!players.TryGetValue(networkId, out Player player))
            {
                return;
            }

            Team target = Teams.Other(player.Team);
            int own = Count(player.Team) - 1;
            int other = Count(target) + 1;

            if (math.abs(own - other) > 3)
            {
                Notice(networkId, "Team/SwitchDenied");
                return;
            }

            Withdraw(networkId);

            player.Team = target;
            player.Dirty = true;
            party.JoinTeam(networkId, target);

            SendRespawn(networkId, player, default);
            SendAllSlots(networkId, target);
        }

        /// <summary>
        /// 부활 요청 검증. 부활 대기가 끝났고 그 분류에 빈자리가 있어야 함. 실패하면 리스폰 창을 다시 띄움.
        /// </summary>
        public bool TrySpawn(int networkId, SpaceshipId spaceship, out float3 position, out quaternion rotation)
        {
            position = float3.zero;
            rotation = quaternion.identity;

            if (!players.TryGetValue(networkId, out Player player))
            {
                return false;
            }

            if (time < player.RespawnAt
                || !TryGetLimit(spaceship, out ShipLimitElement limit)
                || Occupied(player.Team, limit.Category) >= limit.Max)
            {
                SendRespawn(networkId, player, default);
                return false;
            }

            GameModeComponent mode = Mode;
            float2 center = player.Team == Team.Red ? mode.RedSpawn : mode.BlueSpawn;
            float2 direction = random.NextFloat2Direction();
            float distance = random.NextFloat(0.0F, mode.SpawnRadius);

            // 대장선이 있으면 대장선 근처에서 부활함.
            Entity king = FindKing(player.Team);

            if (king != Entity.Null)
            {
                center = entityManager.GetComponentData<LocalTransform>(king).Position.xy;
                distance = entityManager.GetComponentData<SpaceshipSpecComponent>(king).Thickness * 0.5F
                           + random.NextFloat(KingSpawnGap.x, KingSpawnGap.y);
            }

            // 경계에 붙은 부활 지점에서도 전투 구역 안에서 나옴.
            float2 half = mode.MapSize * 0.5F;
            position = new float3(math.clamp(center + direction * distance, -half, half), 0.0F);

            // 우리 진영 쪽(원점 반대)이 아니라 전장 가운데를 바라봄.
            float2 forward = math.normalizesafe(-center, new float2(0.0F, 1.0F));
            rotation = quaternion.RotateZ(math.atan2(-forward.x, forward.y));

            player.Spaceship = spaceship;
            player.OutOfZone = 0.0F;
            player.Dirty = true;

            return true;
        }

        /// <summary>
        /// 플레이어가 찍은 적대 핑. 적 대장선을 가리키면 같은 팀 대장선이 그 위치를 앎.
        /// </summary>
        public void ReportPing(int networkId, float2 position)
        {
            if (!players.TryGetValue(networkId, out Player player))
            {
                return;
            }

            Entity king = FindKing(player.Team);
            Entity enemy = FindKing(Teams.Other(player.Team));

            if (king == Entity.Null || enemy == Entity.Null)
            {
                return;
            }

            float2 enemyPosition = entityManager.GetComponentData<LocalTransform>(enemy).Position.xy;
            float reach = entityManager.GetComponentData<SpaceshipSpecComponent>(enemy).Thickness;

            if (math.distancesq(position, enemyPosition) > reach * reach)
            {
                return;
            }

            KingCarrierComponent state = entityManager.GetComponentData<KingCarrierComponent>(king);
            state.EnemyPosition = enemyPosition;
            state.EnemySighted = false;
            state.KnownUntil = math.max(state.KnownUntil, time + KingCarrierComponent.PingMemory);
            entityManager.SetComponentData(king, state);
        }

        /// <summary>
        /// 대장선의 함재기가 적 대장선을 찾음. 팀원 모두에게 핑을 찍어 줌. 일정 간격으로만 다시 찍음.
        /// </summary>
        public void ReportCraftSight(Team team, float2 position, SpaceshipId enemy)
        {
            if (craftPings.TryGetValue(team, out double last) && time - last < CraftPingInterval)
            {
                return;
            }

            craftPings[team] = time;

            SendTeam(team, new PingRPC
            {
                Position        = position,
                Target          = PingTarget.Spaceship,
                TargetId        = (ushort)enemy,
                Hostile         = true,
                Sender          = KingName(team),
                SenderNetworkId = KingSender(team),
            });
        }

        /// <summary>
        /// 기함이 상대 팀에게 공격받는 중. 그 팀 플레이어에게 경보를 보냄.
        /// </summary>
        public void AlertFlagshipAttacked(Team team)
        {
            SendTeam(team, new FlagshipAttackedRPC());
        }

        /// <summary>
        /// 대장선의 함재기가 모두 사라짐. 함재기가 찍은 핑을 지움.
        /// </summary>
        public void ClearCraftSight(Team team)
        {
            if (!craftPings.Remove(team))
            {
                return;
            }

            SendTeam(team, new PingRPC { Remove = true, Sender = KingName(team), SenderNetworkId = KingSender(team) });
        }

        // 핑을 보낸 대장선을 플레이어와 겹치지 않는 음수로 구분함.
        private static int KingSender(Team team)
        {
            return -(int)team;
        }

        private static FixedString64Bytes KingName(Team team)
        {
            return team == Team.Red ? "Red Flagship" : "Blue Flagship";
        }

        // 새로 입장한 플레이어를 인원이 적은 팀에 넣음.
        private void Join()
        {
            using NativeArray<Entity> connections = connectionQuery.ToEntityArray(Allocator.Temp);

            foreach (Entity connection in connections)
            {
                int networkId = entityManager.GetComponentData<NetworkId>(connection).Value;

                if (players.ContainsKey(networkId))
                {
                    continue;
                }

                int red = Count(Team.Red);
                int blue = Count(Team.Blue);

                var player = new Player
                {
                    Team = red < blue ? Team.Red : blue < red ? Team.Blue : random.NextBool() ? Team.Red : Team.Blue,
                    Name = entityManager.GetComponentData<PlayerNameComponent>(connection).Value,
                    RespawnAt = time,
                    Dirty = true,
                };

                // 기존 플레이어 점수판과 팀 점수를 먼저 보냄.
                foreach (var (id, other) in players)
                {
                    Send(MakeStat(id, other), connection);
                }

                Send(new TeamScoreRPC
                {
                    Red       = redScore,
                    Blue      = blueScore,
                    Winner    = winner,
                    RestartIn = winner != Team.None ? (float)math.max(restartAt - time, 0.0) : 0.0F,
                }, connection);

                players[networkId] = player;
                party.JoinTeam(networkId, player.Team);

                SendRespawn(networkId, player, default);
                SendAllSlots(networkId, player.Team);
            }
        }

        // 기종 기록과 전투 구역 이탈.
        private void UpdateShips(float deltaTime)
        {
            GameModeComponent mode = Mode;

            using NativeArray<Entity> ships = shipQuery.ToEntityArray(Allocator.Temp);

            foreach (Entity ship in ships)
            {
                int networkId = entityManager.GetComponentData<GhostOwner>(ship).NetworkId;

                if (!players.TryGetValue(networkId, out Player player))
                {
                    continue;
                }

                SpaceshipId kind = entityManager.GetComponentData<SpaceshipKindComponent>(ship).Value;

                if (player.Spaceship != kind)
                {
                    player.Spaceship = kind;
                    player.Dirty = true;
                }

                if (mode.Contains(entityManager.GetComponentData<LocalTransform>(ship).Position.xy))
                {
                    player.OutOfZone = 0.0F;
                    continue;
                }

                player.OutOfZone += deltaTime;

                SpaceshipSpecComponent spec = entityManager.GetComponentData<SpaceshipSpecComponent>(ship);

                if (player.OutOfZone < mode.OutOfZoneTime || spec.Health <= 0.0F)
                {
                    continue;
                }

                // 막타 없음. 킬 로그에는 격추된 쪽만 남음. 격추 판정 전에 자가 수리가 체력을 되살리지 않게 수리를 멈춤.
                spec.Health = 0.0F;
                spec.RepairCooldown = math.max(spec.RepairCooldown, 1.0F);
                entityManager.SetComponentData(ship, spec);
                entityManager.SetComponentData(ship, new KillingBlowComponent());
            }
        }

        // 새 판. 대장선, 위성을 새로 놓고 점수와 처치 기록을 지움. 살아 있는 플레이어는 다시 고르게 함.
        private void StartRound()
        {
            bool restart = roundStarted;

            roundStarted = true;
            winner = Team.None;
            redScore = 0;
            blueScore = 0;
            wrecks.Clear();

            DestroyAll(kingQuery);
            DestroyAll(satelliteQuery);

            foreach (Team team in new List<Team>(craftPings.Keys))
            {
                ClearCraftSight(team);
            }

            if (kingConfigQuery.TryGetSingleton(out KingCarrierConfigComponent kings))
            {
                SpawnKings(kings);
            }

            if (!conquestQuery.IsEmpty)
            {
                SpawnSatellites(conquestQuery.GetSingleton<ConquestConfigComponent>(), conquestQuery.GetSingletonBuffer<SatelliteSpawnElement>(true));
            }

            if (restart)
            {
                // 맵을 다시 만듦. 지난 판의 AI 적, 함재기, 아이템, 투사체를 지움. 적은 스폰 시스템이 다시 채움.
                DestroyAll(enemyQuery);
                DestroyAll(allCraftQuery);
                DestroyAll(itemQuery);
                DestroyAll(projectileQuery);
                DestroyAll(sharedProjectileQuery);

                foreach (var (networkId, player) in players)
                {
                    player.Kills = 0;
                    player.Assists = 0;
                    player.Deaths = 0;
                    player.Dirty = true;

                    if (Withdraw(networkId))
                    {
                        SendRespawn(networkId, player, default);
                    }
                }
            }

            Send(new TeamScoreRPC { NewRound = restart }, Entity.Null);
        }

        // 쿼리에 걸린 우주선을 자식(LinkedEntityGroup)까지 지움.
        // DestroyEntity(EntityQuery)는 쿼리에 없는 자식(e.g. 포탑)이 딸려 있으면 예외를 던지므로 배열로 지움.
        private void DestroyAll(EntityQuery query)
        {
            using NativeArray<Entity> entities = query.ToEntityArray(Allocator.Temp);
            entityManager.DestroyEntity(entities);
        }

        private void SpawnKings(in KingCarrierConfigComponent config)
        {
            GameModeComponent mode = Mode;
            float2 half = mode.MapSize * 0.5F;

            // 서로 충분히 떨어진 두 지점. 못 찾으면 가장 멀었던 후보를 씀.
            float2 red = float2.zero;
            float2 blue = float2.zero;
            float farthest = -1.0F;

            for (int i = 0; i < 256 && farthest < config.MinDistance; i++)
            {
                float2 a = random.NextFloat2(-half * 0.8F, half * 0.8F);
                float2 b = random.NextFloat2(-half * 0.8F, half * 0.8F);
                float distance = math.distance(a, b);

                if (distance > farthest)
                {
                    red = a;
                    blue = b;
                    farthest = distance;
                }
            }

            SpawnKing(config, Team.Red, config.RedKing, red);
            SpawnKing(config, Team.Blue, config.BlueKing, blue);
        }

        private void SpawnKing(in KingCarrierConfigComponent config, Team team, SpaceshipId kind, float2 position)
        {
            Entity prefab = registryQuery.GetSingletonBuffer<SpaceshipRegistryElement>(true).Find(kind);

            if (prefab == Entity.Null || !entityManager.HasComponent<CarrierComponent>(prefab))
            {
                return;
            }

            Entity king = entityManager.Instantiate(prefab);

            LocalTransform transform = entityManager.GetComponentData<LocalTransform>(king);
            transform.Position = new float3(position, 0.0F);
            entityManager.SetComponentData(king, transform);

            entityManager.SetComponentData(king, new GhostOwner { NetworkId = -1 });
            entityManager.SetComponentData(king, new PlayerNameComponent { Value = KingName(team) });

            FactionComponent faction = entityManager.GetComponentData<FactionComponent>(king);
            faction.Party = Teams.Party(team);
            entityManager.SetComponentData(king, faction);

            // 최대 체력과 수리는 강화 기본값에서 매 틱 계산됨.
            if (entityManager.HasBuffer<UpgradeParamElement>(king))
            {
                DynamicBuffer<UpgradeParamElement> parameters = entityManager.GetBuffer<UpgradeParamElement>(king);
                parameters.ElementAt((int)UpgradeParam.MaxHealth).Base = config.MaxHealth;
                parameters.ElementAt((int)UpgradeParam.Repair).Base = config.Repair;
            }

            SpaceshipSpecComponent spec = entityManager.GetComponentData<SpaceshipSpecComponent>(king);
            spec.MaxHealth = config.MaxHealth;
            spec.Health = config.MaxHealth;
            spec.Repair = config.Repair;
            entityManager.SetComponentData(king, spec);

            DynamicBuffer<AutoTurretElement> turrets = entityManager.GetBuffer<AutoTurretElement>(king);

            for (int i = 0; i < turrets.Length; i++)
            {
                turrets.ElementAt(i).Turret = config.Turret;
            }

            int capacity = config.SquadSize * config.MaxSquads;
            CarrierComponent carrier = entityManager.GetComponentData<CarrierComponent>(king);
            carrier.Capacity = capacity;
            carrier.Stored = (byte)capacity;
            carrier.Total = (byte)capacity;
            entityManager.SetComponentData(king, carrier);

            // 팀원이 체력과 위치를 늘 볼 수 있게 거리와 무관하게 복제함.
            entityManager.AddComponent<AlwaysRelevantTag>(king);

            entityManager.AddComponentData(king, new KingCarrierComponent
            {
                Team            = team,
                Direction       = random.NextFloat2Direction(),
                LaunchRemaining = config.LaunchCooldown,
                LastHealth      = config.MaxHealth,
                LastHitTime     = double.MinValue,
            });

            DynamicBuffer<KingSquadElement> squads = entityManager.AddBuffer<KingSquadElement>(king);
            squads.Resize(config.MaxSquads, NativeArrayOptions.ClearMemory);
        }

        private void SpawnSatellites(in ConquestConfigComponent config, DynamicBuffer<SatelliteSpawnElement> spawns)
        {
            if (config.SatellitePrefab == Entity.Null)
            {
                return;
            }

            using NativeArray<SatelliteSpawnElement> positions = spawns.ToNativeArray(Allocator.Temp);

            for (int i = 0; i < positions.Length; i++)
            {
                Entity satellite = entityManager.Instantiate(config.SatellitePrefab);

                LocalTransform transform = entityManager.GetComponentData<LocalTransform>(satellite);
                transform.Position = new float3(positions[i].Position, 0.0F);
                entityManager.SetComponentData(satellite, transform);

                entityManager.SetComponentData(satellite, new GhostOwner { NetworkId = -1 });
                entityManager.SetComponentData(satellite, new SatelliteComponent
                {
                    Index = (byte)i,
                    Home  = positions[i].Position,
                });

                ReviveTurret(satellite, config, Team.None);
            }
        }

        // 위성 포탑을 다시 켬. 점령한 팀 편이 되고, 아무도 점령하지 않았으면 모두를 적으로 봄.
        private void ReviveTurret(Entity satellite, in ConquestConfigComponent config, Team owner)
        {
            SpaceshipSpecComponent spec = entityManager.GetComponentData<SpaceshipSpecComponent>(satellite);
            spec.MaxHealth = config.TurretHealth;
            spec.Health = config.TurretHealth;
            spec.RepairCooldown = 0.0F;
            entityManager.SetComponentData(satellite, spec);

            entityManager.SetComponentData(satellite, new FactionComponent
            {
                Value = owner == Team.None ? FactionID.Neutralize : FactionID.Player,
                Party = owner == Team.None ? 0u : Teams.Party(owner),
            });

            DynamicBuffer<AutoTurretElement> turrets = entityManager.GetBuffer<AutoTurretElement>(satellite);

            for (int i = 0; i < turrets.Length; i++)
            {
                turrets.ElementAt(i).Turret = config.SatelliteTurret;
            }
        }

        // 위성 포탑 파괴, 점령, 점수.
        private void UpdateSatellites(float deltaTime)
        {
            if (conquestQuery.IsEmpty)
            {
                return;
            }

            ConquestConfigComponent config = conquestQuery.GetSingleton<ConquestConfigComponent>();

            using NativeArray<Entity> satellites = satelliteQuery.ToEntityArray(Allocator.Temp);
            using NativeArray<Entity> ships = shipQuery.ToEntityArray(Allocator.Temp);
            using NativeArray<Entity> crafts = craftQuery.ToEntityArray(Allocator.Temp);

            bool scored = false;

            foreach (Entity entity in satellites)
            {
                SatelliteComponent satellite = entityManager.GetComponentData<SatelliteComponent>(entity);
                SpaceshipSpecComponent spec = entityManager.GetComponentData<SpaceshipSpecComponent>(entity);
                FactionComponent faction = entityManager.GetComponentData<FactionComponent>(entity);

                // 부딪혀 밀려도 제자리로 돌아옴.
                LocalTransform transform = entityManager.GetComponentData<LocalTransform>(entity);
                transform.Position = new float3(satellite.Home, transform.Position.z);
                entityManager.SetComponentData(entity, transform);
                entityManager.SetComponentData(entity, new MovementComponent());

                // 포탑이 막 부서짐. 아무도 노리지 않고 쏘지도 않음. 폭발은 모두에게 보여 줌.
                if (spec.Health <= 0.0F && faction.Value != FactionID.None)
                {
                    faction.Value = FactionID.None;
                    entityManager.SetComponentData(entity, faction);

                    DynamicBuffer<AutoTurretElement> turrets = entityManager.GetBuffer<AutoTurretElement>(entity);

                    for (int i = 0; i < turrets.Length; i++)
                    {
                        turrets.ElementAt(i).Turret = ItemId.None;
                        turrets.ElementAt(i).Firing = false;
                    }

                    Send(new SpaceshipDestroyedRPC { Position = satellite.Home, OwnerNetworkId = -1 }, Entity.Null);
                    satellite.ReviveRemaining = config.TurretReviveTime;
                }

                // 부서진 포탑은 일정 시간 뒤 점령한 팀 편으로 다시 켜짐. 아무도 점령하지 않은 위성은 다시 켜지지 않음.
                // 상대 팀이 점령도를 쌓아 둔 동안은 기다림이 멈춤.
                bool capturing = satellite.Capturing != Team.None && satellite.Progress > 0.0F;

                if (spec.Health <= 0.0F && satellite.Owner != Team.None && !capturing)
                {
                    satellite.ReviveRemaining -= deltaTime;

                    if (satellite.ReviveRemaining <= 0.0F)
                    {
                        ReviveTurret(entity, config, satellite.Owner);
                        spec = entityManager.GetComponentData<SpaceshipSpecComponent>(entity);
                    }
                }

                // 포탑이 부서져야 점령할 수 있음. 두 팀이 함께 있으면 멈춤.
                // 플레이어는 기체나 함재기 중 하나라도 범위에 있으면 함재기 수와 관계없이 한 명으로 셈.
                int red = 0;
                int blue = 0;

                if (spec.Health <= 0.0F)
                {
                    occupants.Clear();

                    foreach (Entity ship in ships)
                    {
                        int networkId = entityManager.GetComponentData<GhostOwner>(ship).NetworkId;

                        if (players.ContainsKey(networkId) && InCaptureRange(ship, satellite.Home, config.CaptureRadius))
                        {
                            occupants.Add(networkId);
                        }
                    }

                    foreach (Entity craft in crafts)
                    {
                        CarriedCraftComponent craftState = entityManager.GetComponentData<CarriedCraftComponent>(craft);

                        if (!craftState.Returning
                            && players.ContainsKey(craftState.OwnerNetworkId)
                            && InCaptureRange(craft, satellite.Home, config.CaptureRadius))
                        {
                            occupants.Add(craftState.OwnerNetworkId);
                        }
                    }

                    foreach (int networkId in occupants)
                    {
                        if (players[networkId].Team == Team.Red) red++;
                        else blue++;
                    }
                }

                satellite.Occupier = (red > 0) == (blue > 0) ? Team.None : red > 0 ? Team.Red : Team.Blue;

                if (satellite.Occupier != Team.None)
                {
                    Team team = satellite.Occupier;
                    float amount = config.CaptureRate * math.min(math.max(red, blue), config.MaxCapturers) * deltaTime;

                    // 점령한 팀은 상대가 올려 둔 점령도를 깎기만 함. 다른 팀이 올려 둔 점령도도 먼저 깎음.
                    if (team != satellite.Owner && (satellite.Capturing == team || satellite.Progress <= 0.0F))
                    {
                        satellite.Capturing = team;
                        satellite.Progress += amount;
                    }
                    else
                    {
                        satellite.Progress = math.max(satellite.Progress - amount, 0.0F);

                        if (satellite.Progress <= 0.0F)
                        {
                            satellite.Capturing = Team.None;
                        }
                    }

                    if (satellite.Progress >= SatelliteComponent.FullProgress)
                    {
                        satellite.Owner = team;
                        satellite.Capturing = Team.None;
                        satellite.Progress = 0.0F;
                        satellite.ScoreRemaining = config.ScoreInterval;

                        ReviveTurret(entity, config, team);
                        Broadcast(team == Team.Red ? "Satellite/CapturedRed" : "Satellite/CapturedBlue", SatelliteComponent.Name(satellite.Index));
                    }
                }

                // 점령도를 올리던 팀이 범위를 오래 비우면 점령도가 0 이 됨.
                bool present = satellite.Capturing == Team.Red ? red > 0 : blue > 0;
                satellite.AbsentTime = satellite.Capturing == Team.None || present ? 0.0F : satellite.AbsentTime + deltaTime;

                if (satellite.Capturing != Team.None && satellite.AbsentTime >= config.ProgressResetTime)
                {
                    satellite.Capturing = Team.None;
                    satellite.Progress = 0.0F;
                    satellite.AbsentTime = 0.0F;
                }

                if (satellite.Owner != Team.None)
                {
                    satellite.ScoreRemaining -= deltaTime;

                    if (satellite.ScoreRemaining <= 0.0F)
                    {
                        satellite.ScoreRemaining += config.ScoreInterval;

                        if (satellite.Owner == Team.Red) redScore++;
                        else blueScore++;

                        scored = true;
                    }
                }

                entityManager.SetComponentData(entity, satellite);
            }

            if (scored)
            {
                Send(new TeamScoreRPC { Red = redScore, Blue = blueScore }, Entity.Null);
            }
        }

        // 살아 있는 기체가 선체 가장자리까지 점령 범위에 닿았는지.
        private bool InCaptureRange(Entity ship, float2 home, float radius)
        {
            SpaceshipSpecComponent spec = entityManager.GetComponentData<SpaceshipSpecComponent>(ship);
            float reach = radius + spec.Thickness * 0.5F;

            return spec.Health > 0.0F && math.distancesq(entityManager.GetComponentData<LocalTransform>(ship).Position.xy, home) <= reach * reach;
        }

        // 대장선이 사라지거나 점수에 닿으면 승리.
        private void CheckWinner()
        {
            GameModeComponent mode = Mode;

            if (mode.Mode == GameMode.Regicide)
            {
                bool red = FindKing(Team.Red) != Entity.Null;
                bool blue = FindKing(Team.Blue) != Entity.Null;

                winner = red && !blue ? Team.Red : blue && !red ? Team.Blue : Team.None;
            }
            else if (mode.WinScore > 0)
            {
                winner = redScore >= mode.WinScore ? Team.Red : blueScore >= mode.WinScore ? Team.Blue : Team.None;
            }

            if (winner == Team.None)
            {
                return;
            }

            restartAt = time + mode.RestartDelay;
            Send(new TeamScoreRPC { Red = redScore, Blue = blueScore, Winner = winner, RestartIn = mode.RestartDelay }, Entity.Null);
        }

        // 살아 있는 기체와 비지 않은 격추 기체 자리.
        private int Occupied(Team team, SpaceshipCategory category)
        {
            DynamicBuffer<SpaceshipRegistryElement> registry = registryQuery.GetSingletonBuffer<SpaceshipRegistryElement>(true);
            int count = 0;

            using NativeArray<Entity> ships = shipQuery.ToEntityArray(Allocator.Temp);

            foreach (Entity ship in ships)
            {
                SpaceshipId kind = entityManager.GetComponentData<SpaceshipKindComponent>(ship).Value;

                if (players.TryGetValue(entityManager.GetComponentData<GhostOwner>(ship).NetworkId, out Player player)
                    && player.Team == team
                    && (int)kind < registry.Length
                    && registry[(int)kind].Category == category)
                {
                    count++;
                }
            }

            foreach (Wreck wreck in wrecks)
            {
                if (wreck.Team == team && wreck.Category == category)
                {
                    count++;
                }
            }

            return count;
        }

        private bool TryGetLimit(SpaceshipId spaceship, out ShipLimitElement limit)
        {
            DynamicBuffer<SpaceshipRegistryElement> registry = registryQuery.GetSingletonBuffer<SpaceshipRegistryElement>(true);

            limit = default;

            return registry.Find(spaceship) != Entity.Null
                   && modeQuery.GetSingletonBuffer<ShipLimitElement>(true).TryGet(registry[(int)spaceship].Category, out limit);
        }

        private int Count(Team team)
        {
            int count = 0;

            foreach (Player player in players.Values)
            {
                if (player.Team == team)
                {
                    count++;
                }
            }

            return count;
        }

        private Entity FindKing(Team team)
        {
            using NativeArray<Entity> kings = kingQuery.ToEntityArray(Allocator.Temp);

            foreach (Entity king in kings)
            {
                if (entityManager.GetComponentData<KingCarrierComponent>(king).Team == team)
                {
                    return king;
                }
            }

            return Entity.Null;
        }

        // 타고 있는 기체를 연출 없이 치움. 주인은 리스폰 창을 띄움. 기체가 없었으면 false.
        private bool Withdraw(int networkId)
        {
            using NativeArray<Entity> ships = shipQuery.ToEntityArray(Allocator.Temp);

            foreach (Entity ship in ships)
            {
                if (entityManager.GetComponentData<GhostOwner>(ship).NetworkId != networkId)
                {
                    continue;
                }

                if (players.TryGetValue(networkId, out Player player))
                {
                    player.RespawnAt = time;
                }

                Entity connection = Connection(networkId);

                if (connection != Entity.Null)
                {
                    Send(new SpaceshipDestroyedRPC
                    {
                        Position       = entityManager.GetComponentData<LocalTransform>(ship).Position.xy,
                        OwnerNetworkId = networkId,
                    }, connection);
                }

                entityManager.DestroyEntity(ship);
                return true;
            }

            return false;
        }

        private void SendRespawn(int networkId, Player player, FixedString64Bytes killer)
        {
            Entity connection = Connection(networkId);

            if (connection != Entity.Null)
            {
                Send(new RespawnRPC
                {
                    Team   = player.Team,
                    Delay  = (float)math.max(player.RespawnAt - time, 0.0),
                    Killer = killer,
                }, connection);
            }
        }

        // 분류별 기체 수가 바뀌면 그 팀에게 보냄.
        private void SendSlots()
        {
            foreach (ShipLimitElement limit in modeQuery.GetSingletonBuffer<ShipLimitElement>(true).ToNativeArray(Allocator.Temp))
            {
                foreach (Team team in new[] { Team.Red, Team.Blue })
                {
                    int occupied = Occupied(team, limit.Category);

                    if (sentSlots.TryGetValue((team, limit.Category), out int sent) && sent == occupied)
                    {
                        continue;
                    }

                    sentSlots[(team, limit.Category)] = occupied;
                    SendTeam(team, new TeamSlotRPC { Category = limit.Category, Occupied = (byte)occupied, Max = (byte)limit.Max });
                }
            }
        }

        private void SendAllSlots(int networkId, Team team)
        {
            Entity connection = Connection(networkId);

            if (connection == Entity.Null)
            {
                return;
            }

            foreach (ShipLimitElement limit in modeQuery.GetSingletonBuffer<ShipLimitElement>(true).ToNativeArray(Allocator.Temp))
            {
                Send(new TeamSlotRPC
                {
                    Category = limit.Category,
                    Occupied = (byte)Occupied(team, limit.Category),
                    Max      = (byte)limit.Max,
                }, connection);
            }
        }

        // 바뀐 점수판 줄을 모두에게 보냄. 응답 시간은 일정 간격으로 크게 바뀐 것만 보냄.
        private void SendStats()
        {
            bool pingTime = time >= nextPingTime;

            if (pingTime)
            {
                nextPingTime = time + PingInterval;
            }

            foreach (var (networkId, player) in players)
            {
                if (pingTime)
                {
                    int ping = Ping(networkId);

                    if (math.abs(ping - player.Ping) >= PingThreshold)
                    {
                        player.Ping = ping;
                        player.Dirty = true;
                    }
                }

                if (!player.Dirty)
                {
                    continue;
                }

                player.Dirty = false;
                Send(MakeStat(networkId, player), Entity.Null);
            }
        }

        private int Ping(int networkId)
        {
            Entity connection = Connection(networkId);

            return connection != Entity.Null && entityManager.HasComponent<NetworkSnapshotAck>(connection)
                ? (int)entityManager.GetComponentData<NetworkSnapshotAck>(connection).EstimatedRTT
                : 0;
        }

        private static PlayerStatRPC MakeStat(int networkId, Player player)
        {
            return new PlayerStatRPC
            {
                NetworkId = networkId,
                Name      = player.Name,
                Team      = player.Team,
                Spaceship = player.Spaceship,
                Kills     = (ushort)player.Kills,
                Assists   = (ushort)player.Assists,
                Deaths    = (ushort)player.Deaths,
                Ping      = (ushort)math.clamp(player.Ping, 0, ushort.MaxValue),
            };
        }

        // 입장한 플레이어의 Connection. 없으면 Entity.Null.
        private Entity Connection(int networkId)
        {
            using NativeArray<Entity> connections = connectionQuery.ToEntityArray(Allocator.Temp);

            foreach (Entity connection in connections)
            {
                if (entityManager.GetComponentData<NetworkId>(connection).Value == networkId)
                {
                    return connection;
                }
            }

            return Entity.Null;
        }

        // 서버는 언어를 모르므로 번역 키와 번역문의 {0} 만 보냄.
        private void Notice(int networkId, string key)
        {
            Entity connection = Connection(networkId);

            if (connection != Entity.Null)
            {
                Send(new ChatRPC { Channel = ChatChannel.System, Text = key }, connection);
            }
        }

        // 모두에게 보내는 안내.
        private void Broadcast(string key, string argument)
        {
            var notice = new ChatRPC { Channel = ChatChannel.System, Text = key };
            notice.Target.CopyFromTruncated(argument);

            Send(notice, Entity.Null);
        }

        private void SendTeam<T>(Team team, T rpc) where T : unmanaged, IRpcCommand
        {
            using NativeArray<Entity> connections = connectionQuery.ToEntityArray(Allocator.Temp);

            foreach (Entity connection in connections)
            {
                if (TeamOf(entityManager.GetComponentData<NetworkId>(connection).Value) == team)
                {
                    Send(rpc, connection);
                }
            }
        }

        // Entity.Null 이면 모든 접속에 전송.
        private void Send<T>(T rpc, Entity connection) where T : unmanaged, IRpcCommand
        {
            // 입장한 접속이 없으면 보내지 않음. 들어오는 플레이어는 입장할 때 전체를 받음.
            if (connection == Entity.Null && connectionQuery.IsEmpty)
            {
                return;
            }

            Entity entity = entityManager.CreateEntity();
            entityManager.AddComponentData(entity, rpc);
            entityManager.AddComponentData(entity, new SendRpcCommandRequest { TargetConnection = connection });
        }
    }
}
