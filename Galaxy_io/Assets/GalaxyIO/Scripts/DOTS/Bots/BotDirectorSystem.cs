using System;
using System.Collections.Generic;
using GalaxyIO.DOTS.Components.Actor;
using GalaxyIO.DOTS.Components.Singleton;
using GalaxyIO.DOTS.Networking;
using GalaxyIO.DOTS.Spaceship;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.NetCode;
using Unity.Transforms;
using UnityEngine;
using Random = Unity.Mathematics.Random;

namespace GalaxyIO.DOTS.Bots
{
    /// <summary>
    /// 자유 모드 서버 봇의 무리를 만들고, 전멸하면 대기 시간 뒤 다시 만드는 시스템. 서버만.
    /// 봇은 플레이어 우주선 프리팹을 주인 없이(-1) 만들고 서버가 입력을 넣어 조종함(대장선과 같은 방식).
    /// 새 Ghost, RPC 를 만들지 않으므로 기존 클라이언트와 그대로 호환됨.
    /// </summary>
    [WorldSystemFilter(WorldSystemFilterFlags.ServerSimulation)]
    [UpdateInGroup(typeof(SimulationSystemGroup), OrderFirst = true)]
    [UpdateAfter(typeof(GhostSimulationSystemGroup))]
    [UpdateBefore(typeof(PredictedSimulationSystemGroup))]
    public partial struct BotDirectorSystem : ISystem
    {
        // 상점에서 파는 포탑. 무작위 장착 후보.
        private static readonly ItemId[] ShopTurrets =
        {
            ItemId.Bore, ItemId.Brakkar, ItemId.Pyron, ItemId.Raptor, ItemId.Veylance, ItemId.Vraxx,
        };

        private bool initialized;
        private Random random;
        private float reportRemaining;

        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<NetworkTime>();
            state.RequireForUpdate<SpaceshipRegistryElement>();
            state.RequireForUpdate<ItemRegistryElement>();
            random = Random.CreateFromIndex((uint)Environment.TickCount & 0x7FFFFFFFu);
        }

        public void OnUpdate(ref SystemState state)
        {
            if (!initialized)
            {
                initialized = true;

                BotSettings settings = DedicatedServerConfig.Current?.Bots;

                // 자유 모드 데디케이티드 서버에서만. 팀 모드 씬(GameModeComponent)이 있으면 돌지 않음.
                if (settings == null || !settings.Enabled || GameSession.Mode != GameMode.Free
                    || SystemAPI.HasSingleton<GameModeComponent>())
                {
                    state.Enabled = false;
                    return;
                }

                CreateGroups(ref state, settings);
                FillDamageTable(settings);
                BotRuntime.PlayerSight = CorvetteSight(ref state) * Math.Max(settings.PlayerSightScale, 0.0F);
                Debug.Log($"[Bots] 플레이어 발견 거리 상한(초계함 화면 기준): {BotRuntime.PlayerSight:0}");
            }

            BotSettings config = DedicatedServerConfig.Current.Bots;
            float deltaTime = SystemAPI.Time.DeltaTime;


            // 무리별 생존 수
            var alive = new NativeHashMap<Entity, int>(32, Allocator.Temp);

            foreach (var (bot, spec) in SystemAPI.Query<RefRO<BotComponent>, RefRO<SpaceshipSpecComponent>>())
            {
                if (spec.ValueRO.Health <= 0.0F)
                {
                    continue;
                }

                alive.TryGetValue(bot.ValueRO.Group, out int count);
                alive[bot.ValueRO.Group] = count + 1;
            }

            var spawns = new NativeList<Entity>(4, Allocator.Temp);
            var idles = new NativeList<Entity>(4, Allocator.Temp);
            var refills = new NativeList<Entity>(2, Allocator.Temp);
            var fleetRefills = new NativeList<Entity>(4, Allocator.Temp);
            double now = SystemAPI.Time.ElapsedTime;

            foreach (var (groupRef, entity) in SystemAPI.Query<RefRW<BotGroupComponent>>().WithEntityAccess())
            {
                ref BotGroupComponent group = ref groupRef.ValueRW;
                alive.TryGetValue(entity, out group.Alive);

                // 기지: 전멸하지 않았으면 오래 전투가 없을 때 격추된 함선만 다시 만듦.
                if (group.Behavior == BotBehavior.Base && group.Spawned && group.Alive > 0
                    && config.BaseRefill > 0.0F
                    && group.Alive < SystemAPI.GetBuffer<BotMemberElement>(entity).Length
                    && now - group.LastCombatTime > config.BaseRefill)
                {
                    refills.Add(entity);
                }

                // 무리: 전멸하지 않았으면 전투 없이 함급별 간격마다 격추된 함선을 한 척씩 다시 만듦.
                if (group.Behavior != BotBehavior.Base && group.Spawned && group.Alive > 0
                    && group.Alive < SystemAPI.GetBuffer<BotMemberElement>(entity).Length
                    && RefillDue(group, config, now))
                {
                    fleetRefills.Add(entity);
                }

                // 오래 전투가 없었으면 지우고 새로 뽑아 다시 만듦.
                if (group.Spawned && group.Alive > 0)
                {
                    BotGroupSettings settings = config.Groups[group.Index];
                    float idleLimit = settings.IdleRespawn >= 0.0F ? settings.IdleRespawn : config.IdleRespawn;

                    if (group.Behavior == BotBehavior.Base && settings.IdleRespawn < 0.0F)
                    {
                        idleLimit = 0.0F;
                    }

                    if (idleLimit > 0.0F && now - group.LastCombatTime > idleLimit)
                    {
                        idles.Add(entity);
                        continue;
                    }
                }

                if (group.Spawned && group.Alive == 0)
                {
                    BotGroupSettings settings = config.Groups[group.Index];
                    group.Spawned = false;
                    group.RespawnRemaining = settings.RespawnDelay >= 0.0F ? settings.RespawnDelay : config.RespawnDelay;
                    group.HasWaypoint = false;
                    group.Target = Entity.Null;
                }

                if (group.Spawned)
                {
                    continue;
                }

                group.RespawnRemaining -= deltaTime;

                if (group.RespawnRemaining <= 0.0F)
                {
                    spawns.Add(entity);
                }
            }

            // 구조 변경은 순회가 끝난 뒤.
            foreach (Entity groupEntity in idles)
            {
                Despawn(ref state, groupEntity);

                BotGroupComponent group = state.EntityManager.GetComponentData<BotGroupComponent>(groupEntity);
                Debug.Log($"[Bots] {config.Groups[group.Index].Name}: 전투 없이 오래 지나 새로 편성함.");

                group.Spawned = false;
                group.Alive = 0;
                group.RespawnRemaining = 0.0F;
                group.HasWaypoint = false;
                group.Target = Entity.Null;
                state.EntityManager.SetComponentData(groupEntity, group);
                spawns.Add(groupEntity);
            }

            // 리스폰 지점 주위에 있으면 안 되는 플레이어 위치. 살아 있는 우주선과 우주선 없는 접속(관찰자 제외)의 시점.
            var players = new NativeList<float2>(8, Allocator.Temp);

            if (spawns.Length > 0 || refills.Length > 0 || fleetRefills.Length > 0)
            {
                foreach (var (owner, transform) in
                         SystemAPI.Query<RefRO<GhostOwner>, RefRO<LocalTransform>>().WithAll<PlayerSpaceshipMovementComponent>())
                {
                    if (owner.ValueRO.NetworkId > 0)
                    {
                        players.Add(transform.ValueRO.Position.xy);
                    }
                }

                foreach (RefRO<GhostConnectionPosition> position in
                         SystemAPI.Query<RefRO<GhostConnectionPosition>>()
                             .WithAll<NetworkStreamInGame>()
                             .WithNone<Spectator.SpectatorTag>())
                {
                    players.Add(position.ValueRO.Position.xy);
                }
            }

            foreach (Entity groupEntity in spawns)
            {
                SpawnGroup(ref state, groupEntity, config, players);
            }

            foreach (Entity groupEntity in refills)
            {
                RefillBase(ref state, groupEntity, config);
            }

            foreach (Entity groupEntity in fleetRefills)
            {
                RefillFleet(ref state, groupEntity, config, players, now);
            }

            fleetRefills.Dispose();
            players.Dispose();
            refills.Dispose();
            idles.Dispose();

            // 1분마다 상태 요약
            reportRemaining -= deltaTime;

            if (reportRemaining <= 0.0F)
            {
                reportRemaining = 60.0F;
                int engaged = 0;
                int total = 0;

                foreach (RefRO<BotComponent> bot in SystemAPI.Query<RefRO<BotComponent>>())
                {
                    total++;
                    engaged += bot.ValueRO.State == BotState.Engage ? 1 : 0;
                }

                var summary = new System.Text.StringBuilder($"[Bots] 상태: 봇 {total}척, 교전 {engaged}척 |");

                foreach (RefRO<BotGroupComponent> group in SystemAPI.Query<RefRO<BotGroupComponent>>())
                {
                    float2 at = group.ValueRO.LeaderPosition;
                    summary.Append($" {config.Groups[group.ValueRO.Index].Name}={group.ValueRO.Alive}@({at.x:0},{at.y:0})");

                    if (!group.ValueRO.Spawned)
                    {
                        summary.Append($"(재생성 {group.ValueRO.RespawnRemaining:0}초)");
                    }
                }

                Debug.Log(summary.ToString());
            }

            spawns.Dispose();
            alive.Dispose();
        }

        /// <summary>
        /// 피해 배율 표(봇 무리별, 플레이어, AI 적, 박치기 무리)를 채움. ProjectileDamageSystem, SpaceshipCollisionSystem(Burst)이 읽음.
        /// </summary>
        private static void FillDamageTable(BotSettings settings)
        {
            DedicatedServerConfig.EnemySettings enemies = DedicatedServerConfig.Current?.Enemies;

            var table = new BotDamageTable
            {
                Enabled = true,
                Bot = settings.DamageScale > 0.0F ? settings.DamageScale : 1.0F,
                Player = settings.PlayerDamageScale > 0.0F ? settings.PlayerDamageScale : 1.0F,
                Enemy = enemies != null && enemies.DamageScale > 0.0F ? enemies.DamageScale : 1.0F,
                Ram = Math.Max(settings.RammerRamScale, 0.0F),
                CraftRam = Math.Max(settings.RammerCraftRamScale, 0.0F),
            };

            for (int i = 0; i < settings.Groups.Count && table.Groups.Length < table.Groups.Capacity; i++)
            {
                BotGroupSettings group = settings.Groups[i];
                table.Groups.Add(group.DamageScale > 0.0F ? group.DamageScale : 1.0F);

                if (group.CraftsRam && i < 64)
                {
                    table.RamGroups |= 1UL << i;
                }
            }

            BotDamage.Table.Data = table;
        }

        /// <summary>
        /// 초계함(Vex, Vulture, Nighthawk) 플레이어가 화면에 보는 거리: 최대 시야 크기(세로 반폭) x 16:9 가로 비율 중 가장 큰 것.
        /// </summary>
        private float CorvetteSight(ref SystemState state)
        {
            DynamicBuffer<SpaceshipRegistryElement> registry = SystemAPI.GetSingletonBuffer<SpaceshipRegistryElement>(true);
            float size = 0.0F;

            foreach (SpaceshipId kind in new[] { SpaceshipId.Vex, SpaceshipId.Vulture, SpaceshipId.Nighthawk })
            {
                Entity prefab = registry.Find(kind);

                if (prefab != Entity.Null && state.EntityManager.HasComponent<SpaceshipCameraComponent>(prefab))
                {
                    size = math.max(size, state.EntityManager.GetComponentData<SpaceshipCameraComponent>(prefab).MaxOrthographicSize);
                }
            }

            return size * 16.0F / 9.0F;
        }

        private void CreateGroups(ref SystemState state, BotSettings settings)
        {
            for (int i = 0; i < settings.Groups.Count; i++)
            {
                BotGroupSettings group = settings.Groups[i];

                if (!Enum.TryParse(group.Behavior, true, out BotBehavior behavior))
                {
                    Debug.LogError($"[Bots] 무리 {group.Name}: 알 수 없는 행동 {group.Behavior}. Patrol 로 둠.");
                    behavior = BotBehavior.Patrol;
                }

                Entity entity = state.EntityManager.CreateEntity();

                float2 center = new float2(group.CenterX, group.CenterY);

                state.EntityManager.AddComponentData(entity, new BotGroupComponent
                {
                    Index = i,
                    Behavior = behavior,
                    Fly = string.Equals(group.Move, "Fly", StringComparison.OrdinalIgnoreCase),
                    Party = BotRules.Party(i),
                    Center = center,
                    Radius = math.max(group.Radius, 0.0F),
                    Heading = random.NextFloat2Direction(),
                    RespawnRemaining = 0.0F,
                });

                DynamicBuffer<BotDropElement> drops = state.EntityManager.AddBuffer<BotDropElement>(entity);

                foreach (BotDropSettings drop in group.Drops)
                {
                    if (!Enum.TryParse(drop.Item, true, out ItemId item) || item == ItemId.None)
                    {
                        Debug.LogError($"[Bots] 무리 {group.Name}: 알 수 없는 드랍 아이템 {drop.Item}.");
                        continue;
                    }

                    drops.Add(new BotDropElement { Item = item, Min = drop.Min, Max = drop.Max, Chance = drop.Chance });
                }

                state.EntityManager.AddBuffer<BotMemberElement>(entity);

                DynamicBuffer<BotRoutePoint> route = state.EntityManager.AddBuffer<BotRoutePoint>(entity);

                foreach (BotPoint point in group.Route)
                {
                    route.Add(new BotRoutePoint { Value = new float2(point.X, point.Y) });
                }
            }

            Debug.Log($"[Bots] 자유 모드 봇 무리 {settings.Groups.Count}개 준비");

            // 진단: 포탑 레지스트리가 온전한지. 비어 있으면 서버에서 포탑이 쏘지 않음.
            DynamicBuffer<ItemRegistryElement> items = SystemAPI.GetSingletonBuffer<ItemRegistryElement>(true);
            var report = new System.Text.StringBuilder("[Bots] 포탑 레지스트리:");

            foreach (ItemId id in Enum.GetValues(typeof(ItemId)))
            {
                if (id == ItemId.None || (int)id >= items.Length)
                {
                    continue;
                }

                ItemRegistryElement element = items[(int)id];
                report.Append($" {id}(등록 {(items.Contains(id) ? 1 : 0)}, 투사체 {(element.Turret.ProjectilePrefab != Entity.Null ? 1 : 0)})");
            }

            Debug.Log(report.ToString());

            // 진단: 상점(제작법, 함선 가격) 에셋. 비어 있으면 플레이어가 포탑, 함선을 살 수 없음.
            SystemAPI.ManagedAPI.TryGetSingleton(out ItemRegistryAssetComponent itemAssets);
            SystemAPI.ManagedAPI.TryGetSingleton(out SpaceshipRegistryAssetComponent shipAssets);
            Debug.Log("[Bots] 상점 에셋: "
                      + $"아이템 레지스트리 {(itemAssets?.Value != null ? 1 : 0)}, "
                      + $"Bore 제작법 {(itemAssets?.Value?.FindRecipe(ItemId.Bore) != null ? 1 : 0)}, "
                      + $"함선 레지스트리 {(shipAssets?.Value != null ? 1 : 0)}, "
                      + $"Vulture 가격 {(shipAssets?.Value?.Find(SpaceshipId.Vulture) != null ? 1 : 0)}");
        }

        private void SpawnGroup(ref SystemState state, Entity groupEntity, BotSettings config, NativeList<float2> players)
        {
            EntityManager entityManager = state.EntityManager;
            BotGroupComponent group = entityManager.GetComponentData<BotGroupComponent>(groupEntity);
            BotGroupSettings settings = config.Groups[group.Index];

            // 이번에 만들 기종 목록
            var ships = new List<SpaceshipId>();

            foreach (BotMemberSettings member in settings.Members)
            {
                string[] options = BotFormation.SplitList(member.Ships);

                if (options.Length == 0)
                {
                    continue;
                }

                int count = random.NextInt(math.max(member.Min, 0), math.max(member.Max, member.Min) + 1);
                SpaceshipId fixedKind = SpaceshipId.None;

                // 서로 다른 기종: 후보를 섞어 앞에서부터 씀.
                var pool = new List<string>(options);

                if (member.Distinct)
                {
                    for (int i = pool.Count - 1; i > 0; i--)
                    {
                        int j = random.NextInt(i + 1);
                        (pool[i], pool[j]) = (pool[j], pool[i]);
                    }

                    count = math.min(count, pool.Count);
                }

                for (int n = 0; n < count; n++)
                {
                    SpaceshipId kind = fixedKind;

                    if (kind == SpaceshipId.None)
                    {
                        string pick = member.Distinct ? pool[n] : options[random.NextInt(options.Length)];

                        if (!Enum.TryParse(pick, true, out kind))
                        {
                            Debug.LogError($"[Bots] 무리 {settings.Name}: 알 수 없는 기종 {pick}.");
                            continue;
                        }

                        if (member.SameKind)
                        {
                            fixedKind = kind;
                        }
                    }

                    ships.Add(kind);
                }
            }

            DynamicBuffer<SpaceshipRegistryElement> registry = SystemAPI.GetSingletonBuffer<SpaceshipRegistryElement>(true);

            // 진형 간격: 가장 큰 선체 기준
            float spacing = settings.Spacing;

            if (spacing <= 0.0F)
            {
                float thickest = 0.0F;

                foreach (SpaceshipId kind in ships)
                {
                    Entity prefab = registry.Find(kind);

                    if (prefab != Entity.Null)
                    {
                        thickest = math.max(thickest, entityManager.GetComponentData<SpaceshipSpecComponent>(prefab).Thickness);
                    }
                }

                spacing = thickest * 1.4F + 6.0F;
            }

            // 리스폰 위치: 주위에 플레이어가 없는 곳. 기지는 후보 지점 중에서, 루트가 있으면 루트 위에서, 없으면 구역 안에서.
            float2 heading = random.NextFloat2Direction();
            float2 leaderPosition;
            int routeIndex = 0;
            float safe = config.SpawnSafeRadius;

            using NativeArray<BotRoutePoint> route = entityManager.GetBuffer<BotRoutePoint>(groupEntity, true).ToNativeArray(Allocator.Temp);

            // 다른 무리(살아 있는 리더, 기지 중심)와도 떨어진 곳에 만듦.
            List<float2> others = OtherGroupPositions(ref state, groupEntity);

            if (group.Behavior == BotBehavior.Base)
            {
                leaderPosition = PickBasePoint(settings, config, players, others);
                group.Center = leaderPosition;
                entityManager.SetComponentData(groupEntity, group);
            }
            else if (route.Length >= 2)
            {
                leaderPosition = route[0].Value;
                float bestDistance = -1.0F;

                for (int attempt = 0; attempt < 60; attempt++)
                {
                    int segment = random.NextInt(route.Length);
                    int next = (segment + 1) % route.Length;
                    float2 candidate = math.lerp(route[segment].Value, route[next].Value, random.NextFloat());
                    float score = SpawnScore(candidate, players, others, config);

                    if (score > bestDistance)
                    {
                        bestDistance = score;
                        leaderPosition = candidate;
                        routeIndex = next;
                        heading = math.normalizesafe(route[next].Value - route[segment].Value, heading);
                    }

                    if (score >= 1.0F)
                    {
                        break;
                    }
                }
            }
            else
            {
                leaderPosition = group.Center;
                float bestDistance = -1.0F;

                for (int attempt = 0; attempt < 60; attempt++)
                {
                    float2 candidate = group.Center + random.NextFloat2Direction() * random.NextFloat(0.0F, group.Radius);
                    float score = SpawnScore(candidate, players, others, config);

                    if (score > bestDistance)
                    {
                        bestDistance = score;
                        leaderPosition = candidate;
                    }

                    if (score >= 1.0F)
                    {
                        break;
                    }
                }
            }

            // 격추된 기지 함선을 다시 만들 때 쓰도록 이번 기종을 진형 번호 순서로 기록함.
            DynamicBuffer<BotMemberElement> memberKinds = entityManager.GetBuffer<BotMemberElement>(groupEntity);
            memberKinds.Clear();

            foreach (SpaceshipId kind in ships)
            {
                memberKinds.Add(new BotMemberElement { Ship = kind });
            }

            uint levelSeed = random.NextUInt();
            int spawned = 0;

            for (int slot = 0; slot < ships.Count; slot++)
            {
                float2 offset = BotFormation.Offset(group.Behavior, slot, ships.Count, spacing);
                float2 position;
                float2 forward;

                if (group.Behavior == BotBehavior.Base)
                {
                    // 기지: 엔진을 가운데로 모으고 기수는 바깥을 향한 고리 배치.
                    float angle = math.radians(settings.BaseHeading) + slot * (2.0F * math.PI / ships.Count);
                    forward = new float2(math.sin(angle), math.cos(angle));
                    position = group.Center + forward * settings.BaseRadius;
                }
                else
                {
                    position = leaderPosition + BotRules.Rotate(offset, heading);
                    forward = heading;
                }

                if (SpawnShip(ref state, groupEntity, group, settings, config, ships[slot], slot, offset, position, forward, levelSeed + (uint)slot))
                {
                    spawned++;
                }
            }

            group = entityManager.GetComponentData<BotGroupComponent>(groupEntity);
            group.LastCombatTime = SystemAPI.Time.ElapsedTime;
            group.RouteIndex = routeIndex;
            group.Spawned = spawned > 0;
            group.Alive = spawned;
            group.Spacing = spacing;
            group.Heading = heading;
            group.HasWaypoint = false;
            group.Target = Entity.Null;

            // 만들 수 있는 함선이 없으면 다시 시도하지 않게 길게 쉼.
            group.RespawnRemaining = spawned > 0 ? 0.0F : 600.0F;
            entityManager.SetComponentData(groupEntity, group);

            if (spawned > 0)
            {
                Debug.Log($"[Bots] {settings.Name} 생성: {spawned}척");
            }
        }

        // 가장 가까운 플레이어까지의 거리. 플레이어가 없으면 무한대.
        private static float NearestPlayer(float2 position, NativeList<float2> players)
        {
            float nearest = float.MaxValue;

            foreach (float2 player in players)
            {
                nearest = math.min(nearest, math.distance(position, player));
            }

            return nearest;
        }

        /// <summary>
        /// 다른 무리의 자리: 살아 있는 무리 리더, 기지 중심.
        /// </summary>
        private List<float2> OtherGroupPositions(ref SystemState state, Entity self)
        {
            var positions = new List<float2>();

            foreach (var (group, entity) in SystemAPI.Query<RefRO<BotGroupComponent>>().WithEntityAccess())
            {
                if (entity == self || !group.ValueRO.Spawned || group.ValueRO.Alive <= 0)
                {
                    continue;
                }

                positions.Add(group.ValueRO.Behavior == BotBehavior.Base ? group.ValueRO.Center : group.ValueRO.LeaderPosition);
            }

            return positions;
        }

        /// <summary>
        /// 리스폰 후보 점수. 플레이어와 SpawnSafeRadius, 다른 무리와 SpawnSeparation 넘게 떨어졌으면 1 이상.
        /// </summary>
        private static float SpawnScore(float2 candidate, NativeList<float2> players, List<float2> others, BotSettings config)
        {
            float player = NearestPlayer(candidate, players) / math.max(config.SpawnSafeRadius, 1.0F);
            float other = float.MaxValue;

            foreach (float2 position in others)
            {
                other = math.min(other, math.distance(candidate, position));
            }

            other = config.SpawnSeparation > 0.0F ? other / config.SpawnSeparation : float.MaxValue;
            return math.min(player, other);
        }

        /// <summary>
        /// 기지 위치. 후보 지점 중 주위에 플레이어가 없는 곳을 무작위로 고름.
        /// 모든 후보에 플레이어가 있으면 맵 안 아무 곳(플레이어가 없는 곳을 우선).
        /// </summary>
        private float2 PickBasePoint(BotGroupSettings settings, BotSettings config, NativeList<float2> players, List<float2> others)
        {
            var candidates = new List<float2>();

            foreach (BotPoint point in settings.SpawnPoints)
            {
                candidates.Add(new float2(point.X, point.Y));
            }

            if (candidates.Count == 0)
            {
                candidates.Add(new float2(settings.CenterX, settings.CenterY));
            }

            // 섞어서 앞에서부터 확인
            for (int i = candidates.Count - 1; i > 0; i--)
            {
                int j = random.NextInt(i + 1);
                (candidates[i], candidates[j]) = (candidates[j], candidates[i]);
            }

            // 플레이어도, 다른 무리도 없는 후보 -> 플레이어만 없는 후보 순으로.
            foreach (float2 candidate in candidates)
            {
                if (SpawnScore(candidate, players, others, config) >= 1.0F)
                {
                    return candidate;
                }
            }

            foreach (float2 candidate in candidates)
            {
                if (NearestPlayer(candidate, players) >= config.SpawnSafeRadius)
                {
                    return candidate;
                }
            }

            float2 best = candidates[0];
            float bestDistance = -1.0F;

            for (int attempt = 0; attempt < 40; attempt++)
            {
                float2 candidate = random.NextFloat2Direction() * math.sqrt(random.NextFloat()) * config.MapRadius;
                float distance = NearestPlayer(candidate, players);

                if (distance > bestDistance)
                {
                    bestDistance = distance;
                    best = candidate;
                }

                if (distance >= config.SpawnSafeRadius)
                {
                    break;
                }
            }

            return best;
        }

        private enum HullClass
        {
            Other,
            Corvette,
            Frigate,
            Cruiser,
        }

        private static HullClass ClassOf(SpaceshipId kind)
        {
            switch (kind)
            {
                case SpaceshipId.Vex:
                case SpaceshipId.Vulture:
                case SpaceshipId.Nighthawk:
                    return HullClass.Corvette;
                case SpaceshipId.Vanguard:
                case SpaceshipId.Sentinel:
                case SpaceshipId.Halberd:
                    return HullClass.Frigate;
                case SpaceshipId.Leviathan:
                case SpaceshipId.Paladin:
                case SpaceshipId.Thor:
                    return HullClass.Cruiser;
                default:
                    return HullClass.Other;
            }
        }

        // 함급별 리스폰 간격(초). 0 이면 하지 않음.
        private static float RefillInterval(HullClass hull, BotSettings config)
        {
            return hull switch
            {
                HullClass.Corvette => config.CorvetteRefill,
                HullClass.Frigate => config.FrigateRefill,
                HullClass.Cruiser => config.CruiserRefill,
                _ => 0.0F,
            };
        }

        private static double LastRefill(in BotGroupComponent group, HullClass hull)
        {
            return hull switch
            {
                HullClass.Corvette => group.LastCorvetteRefill,
                HullClass.Frigate => group.LastFrigateRefill,
                HullClass.Cruiser => group.LastCruiserRefill,
                _ => double.MaxValue,
            };
        }

        // 어느 함급이든 전투 없이 리스폰 간격이 지났는지(격추된 함선이 그 함급인지는 RefillFleet 이 봄).
        private static bool RefillDue(in BotGroupComponent group, BotSettings config, double now)
        {
            foreach (HullClass hull in new[] { HullClass.Corvette, HullClass.Frigate, HullClass.Cruiser })
            {
                float interval = RefillInterval(hull, config);

                if (interval > 0.0F && now - math.max(group.LastCombatTime, LastRefill(group, hull)) >= interval)
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// 무리의 격추된 함선을 함급별로 한 척씩 리더 옆 진형 자리에 다시 만듦. 포탑, 강화는 새로 뽑음.
        /// 그 자리 주위에 플레이어가 있으면 다음 기회로 미룸.
        /// </summary>
        private void RefillFleet(ref SystemState state, Entity groupEntity, BotSettings config, NativeList<float2> players, double now)
        {
            EntityManager entityManager = state.EntityManager;
            BotGroupComponent group = entityManager.GetComponentData<BotGroupComponent>(groupEntity);

            if (!group.Spawned || group.Leader == Entity.Null || !entityManager.Exists(group.Leader))
            {
                return;
            }

            BotGroupSettings settings = config.Groups[group.Index];
            var aliveSlots = new HashSet<int>();

            foreach (var (bot, spec) in SystemAPI.Query<RefRO<BotComponent>, RefRO<SpaceshipSpecComponent>>())
            {
                if (bot.ValueRO.Group == groupEntity && spec.ValueRO.Health > 0.0F)
                {
                    aliveSlots.Add(bot.ValueRO.Slot);
                }
            }

            using NativeArray<BotMemberElement> kinds = entityManager.GetBuffer<BotMemberElement>(groupEntity, true).ToNativeArray(Allocator.Temp);
            var refilled = new List<string>();

            foreach (HullClass hull in new[] { HullClass.Corvette, HullClass.Frigate, HullClass.Cruiser })
            {
                float interval = RefillInterval(hull, config);

                if (interval <= 0.0F || now - math.max(group.LastCombatTime, LastRefill(group, hull)) < interval)
                {
                    continue;
                }

                for (int slot = 0; slot < kinds.Length; slot++)
                {
                    if (aliveSlots.Contains(slot) || ClassOf(kinds[slot].Ship) != hull)
                    {
                        continue;
                    }

                    float2 offset = BotFormation.Offset(group.Behavior, slot, kinds.Length, group.Spacing);
                    float2 position = group.LeaderPosition + BotRules.Rotate(offset - group.LeaderOffset, group.Heading);

                    // 플레이어가 주위에 있으면 이번에는 건너뜀(간격은 그대로라 다음 틱에 다시 봄).
                    if (NearestPlayer(position, players) < config.SpawnSafeRadius)
                    {
                        break;
                    }

                    if (SpawnShip(ref state, groupEntity, group, settings, config, kinds[slot].Ship, slot, offset, position, group.Heading, random.NextUInt()))
                    {
                        aliveSlots.Add(slot);
                        refilled.Add(kinds[slot].Ship.ToString());

                        switch (hull)
                        {
                            case HullClass.Corvette: group.LastCorvetteRefill = now; break;
                            case HullClass.Frigate: group.LastFrigateRefill = now; break;
                            case HullClass.Cruiser: group.LastCruiserRefill = now; break;
                        }
                    }

                    // 함급마다 한 번에 한 척만.
                    break;
                }
            }

            if (refilled.Count == 0)
            {
                return;
            }

            BotGroupComponent current = entityManager.GetComponentData<BotGroupComponent>(groupEntity);
            current.LastCorvetteRefill = group.LastCorvetteRefill;
            current.LastFrigateRefill = group.LastFrigateRefill;
            current.LastCruiserRefill = group.LastCruiserRefill;
            current.Alive += refilled.Count;
            entityManager.SetComponentData(groupEntity, current);

            Debug.Log($"[Bots] {settings.Name}: 전투가 없어 {string.Join(", ", refilled)} 다시 합류.");
        }

        /// <summary>
        /// 기지의 격추된 함선만 원래 자리에 다시 만듦. 포탑, 강화는 새로 뽑음.
        /// </summary>
        private void RefillBase(ref SystemState state, Entity groupEntity, BotSettings config)
        {
            EntityManager entityManager = state.EntityManager;
            BotGroupComponent group = entityManager.GetComponentData<BotGroupComponent>(groupEntity);
            BotGroupSettings settings = config.Groups[group.Index];

            var aliveSlots = new HashSet<int>();

            foreach (var (bot, spec) in SystemAPI.Query<RefRO<BotComponent>, RefRO<SpaceshipSpecComponent>>())
            {
                if (bot.ValueRO.Group == groupEntity && spec.ValueRO.Health > 0.0F)
                {
                    aliveSlots.Add(bot.ValueRO.Slot);
                }
            }

            using NativeArray<BotMemberElement> kinds = entityManager.GetBuffer<BotMemberElement>(groupEntity, true).ToNativeArray(Allocator.Temp);
            int refilled = 0;

            for (int slot = 0; slot < kinds.Length; slot++)
            {
                if (aliveSlots.Contains(slot))
                {
                    continue;
                }

                float angle = math.radians(settings.BaseHeading) + slot * (2.0F * math.PI / kinds.Length);
                float2 forward = new float2(math.sin(angle), math.cos(angle));
                float2 position = group.Center + forward * settings.BaseRadius;

                if (SpawnShip(ref state, groupEntity, group, settings, config, kinds[slot].Ship, slot, float2.zero, position, forward, random.NextUInt()))
                {
                    refilled++;
                }
            }

            group = entityManager.GetComponentData<BotGroupComponent>(groupEntity);
            group.LastCombatTime = SystemAPI.Time.ElapsedTime;
            group.Alive += refilled;
            entityManager.SetComponentData(groupEntity, group);

            Debug.Log($"[Bots] {settings.Name}: 전투 없이 오래 지나 격추된 함선 {refilled}척을 다시 만듦.");
        }

        /// <summary>
        /// 무리의 함선과 그 함선들의 함재기를 격추 처리 없이 지움(점수, 킬 로그, 드랍 없음).
        /// </summary>
        private void Despawn(ref SystemState state, Entity groupEntity)
        {
            var members = new NativeList<Entity>(16, Allocator.Temp);
            var memberSet = new NativeHashSet<Entity>(16, Allocator.Temp);

            foreach (var (bot, entity) in SystemAPI.Query<RefRO<BotComponent>>().WithEntityAccess())
            {
                if (bot.ValueRO.Group == groupEntity)
                {
                    members.Add(entity);
                    memberSet.Add(entity);
                }
            }

            var crafts = new NativeList<Entity>(16, Allocator.Temp);

            foreach (var (craft, entity) in SystemAPI.Query<RefRO<CarriedCraftComponent>>().WithEntityAccess())
            {
                if (memberSet.Contains(craft.ValueRO.Carrier))
                {
                    crafts.Add(entity);
                }
            }

            // 자식(포탑 등)까지 지우도록 배열로 지움.
            state.EntityManager.DestroyEntity(crafts.AsArray());
            state.EntityManager.DestroyEntity(members.AsArray());

            crafts.Dispose();
            memberSet.Dispose();
            members.Dispose();
        }

        private bool SpawnShip(
            ref SystemState state,
            Entity groupEntity,
            in BotGroupComponent group,
            BotGroupSettings settings,
            BotSettings config,
            SpaceshipId kind,
            int slot,
            float2 offset,
            float2 position,
            float2 forward,
            uint seed)
        {
            EntityManager entityManager = state.EntityManager;
            Entity prefab = SystemAPI.GetSingletonBuffer<SpaceshipRegistryElement>(true).Find(kind);

            if (prefab == Entity.Null)
            {
                Debug.LogError($"[Bots] {settings.Name}: 레지스트리에 {kind} 프리팹이 없음.");
                return false;
            }

            var shipRandom = Random.CreateFromIndex(seed & 0x7FFFFFFFu);
            Entity ship = entityManager.Instantiate(prefab);

            LocalTransform transform = entityManager.GetComponentData<LocalTransform>(ship);
            transform.Position = new float3(position, 0.0F);
            transform.Rotation = SpaceshipHeading.ToRotation(forward);
            entityManager.SetComponentData(ship, transform);

            entityManager.SetComponentData(ship, new GhostOwner { NetworkId = -1 });
            entityManager.SetComponentData(ship, new PlayerNameComponent { Value = new FixedString64Bytes("[봇]" + settings.Name) });

            FactionComponent faction = entityManager.GetComponentData<FactionComponent>(ship);
            faction.Party = group.Party;
            entityManager.SetComponentData(ship, faction);

            Entity itemRegistryEntity = SystemAPI.GetSingletonEntity<ItemRegistryElement>();

            // 포탑
            ItemId mainTurret = entityManager.GetComponentData<EquippedTurretComponent>(ship).Value;

            if (!entityManager.HasComponent<FixedTurretTag>(ship) && entityManager.GetBuffer<TurretElement>(ship, true).Length > 0)
            {
                // 무리 상한(TurretCaps)에 걸린 포탑은 빼고 고름.
                ItemId chosen = PickTurret(settings.MainTurret, mainTurret, kind, config, ref shipRandom, CappedTurrets(ref state, groupEntity, settings));

                if (chosen != mainTurret)
                {
                    EquippedTurretComponent equipped = entityManager.GetComponentData<EquippedTurretComponent>(ship);
                    TurretEquip.Refresh(
                        chosen,
                        entityManager.GetBuffer<ItemRegistryElement>(itemRegistryEntity, true),
                        ref equipped,
                        entityManager.GetBuffer<TurretCooldownElement>(ship)
                    );
                    entityManager.SetComponentData(ship, equipped);
                    mainTurret = chosen;
                }
            }

            if (entityManager.HasBuffer<AutoTurretElement>(ship))
            {
                DynamicBuffer<AutoTurretElement> autos = entityManager.GetBuffer<AutoTurretElement>(ship);
                DynamicBuffer<AutoTurretMountElement> mounts = entityManager.GetBuffer<AutoTurretMountElement>(ship);
                DynamicBuffer<ItemRegistryElement> items = entityManager.GetBuffer<ItemRegistryElement>(itemRegistryEntity, true);

                for (int i = 0; i < autos.Length && i < mounts.Length; i++)
                {
                    ItemId chosen = PickTurret(settings.AutoTurrets, autos[i].Turret, kind, config, ref shipRandom);

                    if (chosen == autos[i].Turret || chosen == ItemId.None)
                    {
                        continue;
                    }

                    ItemRegistryElement element = items.Get(chosen);

                    autos.ElementAt(i).Turret = chosen;
                    autos.ElementAt(i).Firing = false;
                    mounts.ElementAt(i).Cooldown = element.Turret.MaxFireCooldown;
                    mounts.ElementAt(i).Resting = false;
                    mounts.ElementAt(i).CycleRemaining = element.AutoTurret.FireDuration;
                }
            }

            // 박치기 무리는 자동 포탑 구성을 남겨 확인할 수 있게 함.
            if (settings.CraftsRam && entityManager.HasBuffer<AutoTurretElement>(ship))
            {
                var list = new List<string>();

                foreach (AutoTurretElement auto in entityManager.GetBuffer<AutoTurretElement>(ship, true))
                {
                    list.Add(auto.Turret.ToString());
                }

                Debug.Log($"[Bots] {settings.Name} {kind}: 조종 {mainTurret}, 자동 포탑 {list.Count}칸 [{string.Join(", ", list)}]");
            }

            ItemRegistryElement main = entityManager.GetBuffer<ItemRegistryElement>(itemRegistryEntity, true).Get(mainTurret);

            // 강화
            if (entityManager.HasBuffer<UpgradeElement>(ship) && entityManager.HasBuffer<UpgradeParamElement>(ship))
            {
                ApplyUpgrades(entityManager, ship, settings, config, kind, main, ref shipRandom);
            }

            // 함재기는 평소 모선 호위. 박치기 무리는 평소에 모선 안에 넣어 둠.
            if (entityManager.HasComponent<CarrierComponent>(ship))
            {
                CarrierComponent carrier = entityManager.GetComponentData<CarrierComponent>(ship);
                carrier.Order = settings.CraftsRam ? CarrierOrder.None : CarrierOrder.Escort;
                carrier.Target = Entity.Null;
                carrier.Position = position;
                carrier.Reassign = true;
                entityManager.SetComponentData(ship, carrier);
            }

            WeaponReach(entityManager, main, out float range, out float projectileSpeed);

            // 박치기 함재기 속도: SpeedLike 기종의 최대 강화 비행 속력, 가속도.
            float craftSpeed = 0.0F;
            float craftAcceleration = 0.0F;

            if (settings.CraftsRam && Enum.TryParse(settings.SpeedLike, true, out SpaceshipId craftLike))
            {
                TopSpeed(entityManager, craftLike, out craftSpeed, out craftAcceleration);
            }

            BotBehavior behavior = group.Behavior;

            entityManager.AddComponentData(ship, new BotComponent
            {
                Group = groupEntity,
                Behavior = behavior,
                Slot = slot,
                Offset = offset,
                State = BotState.Formation,
                StrafeSign = shipRandom.NextBool() ? 1.0F : -1.0F,
                WeaponRange = range,
                ProjectileSpeed = projectileSpeed,
                Home = position,
                HomeHeading = forward,
                CraftsRam = settings.CraftsRam,
                CraftSpeed = craftSpeed,
                CraftAcceleration = craftAcceleration,
            });

            return true;
        }

        private static void ApplyUpgrades(
            EntityManager entityManager,
            Entity ship,
            BotGroupSettings settings,
            BotSettings config,
            SpaceshipId kind,
            in ItemRegistryElement main,
            ref Random shipRandom)
        {
            DynamicBuffer<UpgradeElement> limits = entityManager.GetBuffer<UpgradeElement>(ship, true);
            float min = settings.UpgradeMin >= 0.0F ? settings.UpgradeMin : config.UpgradeMin;
            float max = settings.UpgradeMax >= 0.0F ? settings.UpgradeMax : config.UpgradeMax;

            var maxed = new HashSet<UpgradeStat>();

            foreach (string name in BotFormation.SplitList(settings.MaxUpgrades))
            {
                if (Enum.TryParse(name, true, out UpgradeStat stat))
                {
                    maxed.Add(stat);
                }
            }

            var levels = new byte[limits.Length];

            for (int i = 0; i < limits.Length; i++)
            {
                int top = limits[i].MaxLevel;
                levels[i] = maxed.Contains((UpgradeStat)i)
                    ? (byte)top
                    : (byte)math.clamp((int)math.round(shipRandom.NextFloat(math.min(min, max), math.max(min, max)) * top), 0, top);
            }

            DynamicBuffer<UpgradeLevelElement> levelBuffer = entityManager.GetBuffer<UpgradeLevelElement>(ship);

            for (int i = 0; i < levelBuffer.Length && i < levels.Length; i++)
            {
                levelBuffer.ElementAt(i).Value = levels[i];
            }

            DynamicBuffer<UpgradeParamElement> parameters = entityManager.GetBuffer<UpgradeParamElement>(ship);

            // 최대 체력 배율
            if (math.abs(settings.HealthScale - 1.0F) > 0.001F && settings.HealthScale > 0.0F)
            {
                ref UpgradeParamElement health = ref parameters.ElementAt((int)UpgradeParam.MaxHealth);
                health.Base *= settings.HealthScale;
                health.Amount *= settings.HealthScale;
            }

            // 다른 기종의 최대 강화 비행 속력, 가속도
            if (!string.IsNullOrEmpty(settings.SpeedLike)
                && Enum.TryParse(settings.SpeedLike, true, out SpaceshipId like)
                && like != kind)
            {
                CopyTopSpeed(entityManager, like, parameters);
                parameters = entityManager.GetBuffer<UpgradeParamElement>(ship);
            }

            // 조종 포탑 발사 간격 단축. 강화값이 발사 간격에서 빼는 양이므로 간격 x (1 - 배율)을 넣음.
            if (settings.ReloadScale < 1.0F && main.Turret.MaxFireCooldown > 0.0F)
            {
                ref UpgradeParamElement reload = ref parameters.ElementAt((int)UpgradeParam.ReloadTime);
                reload.Base = main.Turret.MaxFireCooldown * (1.0F - math.saturate(settings.ReloadScale));
                reload.Amount = 0.0F;
            }

            if (settings.NoOverheat)
            {
                ref UpgradeParamElement heat = ref parameters.ElementAt((int)UpgradeParam.MaxHeat);
                heat.Base = 1000000.0F;
                heat.Amount = 0.0F;
            }

            // 강화한 최대 체력으로 가득 채워 시작함. 다른 능력치는 SpaceshipUpgradeSystem 이 다음 틱에 반영함.
            DynamicBuffer<UpgradeLevelElement> levelsRO = entityManager.GetBuffer<UpgradeLevelElement>(ship, true);
            float maxHealth = Upgrade.Value(parameters, levelsRO, UpgradeParam.MaxHealth);

            SpaceshipSpecComponent spec = entityManager.GetComponentData<SpaceshipSpecComponent>(ship);
            spec.MaxHealth = maxHealth;
            spec.Health = maxHealth;
            entityManager.SetComponentData(ship, spec);

            if (settings.NoOverheat && entityManager.HasComponent<TurretHeatComponent>(ship))
            {
                TurretHeatComponent heat = entityManager.GetComponentData<TurretHeatComponent>(ship);
                heat.MaxHeat = 1000000.0F;
                entityManager.SetComponentData(ship, heat);
            }
        }

        private static void CopyTopSpeed(EntityManager entityManager, SpaceshipId like, DynamicBuffer<UpgradeParamElement> parameters)
        {
            using EntityQuery query = entityManager.CreateEntityQuery(ComponentType.ReadOnly<SpaceshipRegistryElement>());
            Entity prefab = query.GetSingletonBuffer<SpaceshipRegistryElement>(true).Find(like);

            if (prefab == Entity.Null || !entityManager.HasBuffer<UpgradeParamElement>(prefab))
            {
                return;
            }

            DynamicBuffer<UpgradeParamElement> source = entityManager.GetBuffer<UpgradeParamElement>(prefab, true);
            DynamicBuffer<UpgradeElement> sourceLimits = entityManager.GetBuffer<UpgradeElement>(prefab, true);

            foreach (UpgradeParam param in new[] { UpgradeParam.MaxSpeed, UpgradeParam.Acceleration })
            {
                UpgradeParamElement from = source[(int)param];
                int top = (int)from.Stat < sourceLimits.Length ? sourceLimits[(int)from.Stat].MaxLevel : 0;

                ref UpgradeParamElement to = ref parameters.ElementAt((int)param);
                to.Base = from.Base + from.Amount * top;
                to.Amount = 0.0F;
            }
        }

        // 기종의 최대 강화 비행 속력, 가속도
        private static void TopSpeed(EntityManager entityManager, SpaceshipId kind, out float speed, out float acceleration)
        {
            speed = 0.0F;
            acceleration = 0.0F;

            using EntityQuery query = entityManager.CreateEntityQuery(ComponentType.ReadOnly<SpaceshipRegistryElement>());
            Entity prefab = query.GetSingletonBuffer<SpaceshipRegistryElement>(true).Find(kind);

            if (prefab == Entity.Null || !entityManager.HasBuffer<UpgradeParamElement>(prefab))
            {
                return;
            }

            DynamicBuffer<UpgradeParamElement> parameters = entityManager.GetBuffer<UpgradeParamElement>(prefab, true);
            DynamicBuffer<UpgradeElement> limits = entityManager.GetBuffer<UpgradeElement>(prefab, true);

            UpgradeParamElement maxSpeed = parameters[(int)UpgradeParam.MaxSpeed];
            UpgradeParamElement accel = parameters[(int)UpgradeParam.Acceleration];
            speed = maxSpeed.Base + maxSpeed.Amount * ((int)maxSpeed.Stat < limits.Length ? limits[(int)maxSpeed.Stat].MaxLevel : 0);
            acceleration = accel.Base + accel.Amount * ((int)accel.Stat < limits.Length ? limits[(int)accel.Stat].MaxLevel : 0);
        }

        // 조종 포탑 투사체가 닿는 거리와 평균 탄속
        private static void WeaponReach(EntityManager entityManager, in ItemRegistryElement turret, out float range, out float speed)
        {
            range = 0.0F;
            speed = 0.0F;

            Entity projectile = turret.Turret.ProjectilePrefab;

            if (projectile == Entity.Null
                || !entityManager.HasComponent<SharedProjectileComponent>(projectile)
                || !entityManager.HasComponent<StraightforwardProjectileComponent>(projectile))
            {
                return;
            }

            float lifetime = entityManager.GetComponentData<SharedProjectileComponent>(projectile).Lifetime;
            StraightforwardProjectileComponent motion = entityManager.GetComponentData<StraightforwardProjectileComponent>(projectile);

            float time = lifetime;

            // 감속하는 투사체는 멈추는 시점까지만 감.
            if (motion.Acceleration < 0.0F && motion.Speed > 0.0F)
            {
                time = math.min(time, motion.Speed / -motion.Acceleration);
            }

            range = math.max(motion.Speed * time + 0.5F * motion.Acceleration * time * time, 0.0F);
            speed = lifetime > 0.0F ? range / math.max(time, 0.01F) : motion.Speed;
        }

        /// <summary>
        /// 무리 상한(TurretCaps)에 이미 닿은 조종 포탑. 살아 있는 구성원의 장착 포탑을 셈.
        /// </summary>
        private HashSet<ItemId> CappedTurrets(ref SystemState state, Entity groupEntity, BotGroupSettings settings)
        {
            if (string.IsNullOrEmpty(settings.TurretCaps))
            {
                return null;
            }

            var counts = new Dictionary<ItemId, int>();

            foreach (var (bot, equipped, spec) in
                     SystemAPI.Query<RefRO<BotComponent>, RefRO<EquippedTurretComponent>, RefRO<SpaceshipSpecComponent>>())
            {
                if (bot.ValueRO.Group == groupEntity && spec.ValueRO.Health > 0.0F)
                {
                    counts.TryGetValue(equipped.ValueRO.Value, out int count);
                    counts[equipped.ValueRO.Value] = count + 1;
                }
            }

            var capped = new HashSet<ItemId>();

            foreach (string part in BotFormation.SplitList(settings.TurretCaps))
            {
                string[] pair = part.Split(':');

                if (pair.Length == 2 && Enum.TryParse(pair[0].Trim(), true, out ItemId item) && int.TryParse(pair[1], out int cap)
                    && counts.TryGetValue(item, out int used) && used >= cap)
                {
                    capped.Add(item);
                }
            }

            return capped;
        }

        private ItemId PickTurret(string rule, ItemId current, SpaceshipId kind, BotSettings config, ref Random shipRandom,
            HashSet<ItemId> excluded = null)
        {
            if (string.Equals(rule, "Keep", StringComparison.OrdinalIgnoreCase))
            {
                return current;
            }

            if (string.IsNullOrEmpty(rule))
            {
                // 초계함은 금지 포탑(기본 레이저 Veylance)을 뺀 후보에서 고름.
                var candidates = new List<ItemId>(ShopTurrets);

                if (kind == SpaceshipId.Vex || kind == SpaceshipId.Vulture || kind == SpaceshipId.Nighthawk)
                {
                    foreach (string banned in BotFormation.SplitList(config.CorvetteBannedTurrets))
                    {
                        if (Enum.TryParse(banned, true, out ItemId bannedItem))
                        {
                            candidates.Remove(bannedItem);
                        }
                    }
                }

                if (candidates.Count == 0)
                {
                    candidates.AddRange(ShopTurrets);
                }

                return candidates[shipRandom.NextInt(candidates.Count)];
            }

            if (Enum.TryParse(rule, true, out ItemId item) && item != ItemId.None)
            {
                return item;
            }

            // "아이템|아이템" 또는 "아이템:비중|..." 중 비중대로 하나. 금지 목록(excluded)은 뺌.
            var options = new List<(ItemId Item, float Weight)>();
            float total = 0.0F;

            foreach (string part in BotFormation.SplitList(rule))
            {
                string[] pair = part.Split(':');
                float weight = pair.Length > 1 && float.TryParse(pair[1], System.Globalization.NumberStyles.Float,
                    System.Globalization.CultureInfo.InvariantCulture, out float parsed) ? parsed : 1.0F;

                if (Enum.TryParse(pair[0].Trim(), true, out ItemId option) && option != ItemId.None && weight > 0.0F
                    && (excluded == null || !excluded.Contains(option)))
                {
                    options.Add((option, weight));
                    total += weight;
                }
            }

            if (options.Count > 0)
            {
                float roll = shipRandom.NextFloat(total);

                foreach ((ItemId option, float weight) in options)
                {
                    roll -= weight;

                    if (roll <= 0.0F)
                    {
                        return option;
                    }
                }

                return options[options.Count - 1].Item;
            }

            Debug.LogError($"[Bots] 알 수 없는 포탑 {rule}. 그대로 둠.");
            return current;
        }
    }

    /// <summary>
    /// 무리 진형 자리. 리더 기준 (x = 오른쪽, y = 앞).
    /// </summary>
    public static class BotFormation
    {
        /// <summary>
        /// "A|B|C" 를 나눔. 빈 칸과 앞뒤 공백은 버림.
        /// </summary>
        public static string[] SplitList(string value)
        {
            if (string.IsNullOrEmpty(value))
            {
                return Array.Empty<string>();
            }

            var list = new List<string>();

            foreach (string part in value.Split('|'))
            {
                string trimmed = part.Trim();

                if (trimmed.Length > 0)
                {
                    list.Add(trimmed);
                }
            }

            return list.ToArray();
        }

        public static float2 Offset(BotBehavior behavior, int slot, int count, float spacing)
        {
            if (slot == 0)
            {
                return float2.zero;
            }

            if (behavior == BotBehavior.Line)
            {
                // 일자 횡대. 리더에서 좌우로 번갈아 벌림.
                int rank = (slot + 1) / 2;
                float side = slot % 2 == 1 ? 1.0F : -1.0F;
                return new float2(side * rank * spacing, 0.0F);
            }

            // 큰 떼(13척 이상): 리더 둘레로 벌집처럼 겹겹이.
            if (count > 12)
            {
                int ring = 1;
                int index = slot;

                while (index > ring * 6)
                {
                    index -= ring * 6;
                    ring++;
                }

                float angle = (index - 1) * 2.0F * math.PI / (ring * 6);
                return new float2(math.sin(angle), math.cos(angle)) * ring * spacing * 0.9F;
            }

            // V 자. 리더 뒤로 좌우 번갈아 한 줄씩.
            int row = (slot + 1) / 2;
            float sign = slot % 2 == 1 ? -1.0F : 1.0F;
            return new float2(sign * row * spacing * 0.9F, -row * spacing * 0.8F);
        }
    }
}
