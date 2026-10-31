using GalaxyIO.DOTS.Components.Actor;
using GalaxyIO.DOTS.Components.Singleton;
using GalaxyIO.DOTS.Match;
using GalaxyIO.DOTS.Spaceship;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.NetCode;
using Unity.Transforms;
using Random = Unity.Mathematics.Random;

namespace GalaxyIO.DOTS.Systems
{
    /// <summary>
    /// 대장선 잡기의 대장선 AI. 적 대장선을 아는지, 공격받는지로 탐색 / 추격 / 도주를 고르고
    /// 플레이어 입력(<see cref="PlayerInputComponent"/>)을 서버에서 직접 넣어 조종함.
    /// 편대(<see cref="KingSquadElement"/>)에 명령을 내리고, 함재기가 적 대장선을 찾으면 팀원에게 핑을 찍음.
    /// </summary>
    [WorldSystemFilter(WorldSystemFilterFlags.ServerSimulation)]
    [UpdateInGroup(typeof(SimulationSystemGroup), OrderFirst = true)]
    [UpdateAfter(typeof(GhostSimulationSystemGroup))]
    [UpdateBefore(typeof(PredictedSimulationSystemGroup))]
    [UpdateAfter(typeof(GameModeSystem))]
    public partial struct KingCarrierSystem : ISystem
    {
        // 전투 구역 경계에서 이만큼 안쪽을 벗어나려 하면 방향을 바꿈.
        private const float BoundaryMargin = 200.0F;

        // 커서를 기수 앞 이 거리에 둠.
        private const float CursorDistance = 100.0F;

        // 유지 거리에서 이만큼 벗어나야 다가가거나 물러남.
        private const float DistanceTolerance = 50.0F;

        // 지금 속도로 이 시간(초) 동안 날아간 자리를 도착 예상 지점으로 봄. 돌아서는 데 걸리는 시간보다 길게 둠.
        private const float PredictTime = 6.0F;

        // 맵 이탈 방지. 지금 제동하면 멈추는 지점(제동 거리 + 반응 시간만큼 더 간 거리)이 맵 가장자리에서
        // EnterMargin 안쪽을 넘거나 이미 맵 밖이면 돌아오기 시작함. 맵 가장자리에서 InnerMargin 안쪽 상자의
        // 가장 가까운 점으로 감. 위치와 멈추는 지점이 모두 ExitMargin 안쪽이면 끝냄.
        private const float ReturnReaction = 2.0F;
        private const float EnterMargin = 50.0F;
        private const float ExitMargin = 200.0F;
        private const float InnerMargin = 350.0F;

        // 가려는 방향이 기수에서 이 각도(도) 넘게 벗어나 있으면 가속하지 않고 줄여 가며 돎.
        // 모선은 빠를수록 거의 돌지 못해(ManeuverPenalty) 가속한 채로 돌면 큰 원을 그리며 밀려 나감.
        private const float TurnAccelerationAngle = 30.0F;

        // 상대 팀에게 맞으면 팀에 알림. 이 간격(초)마다 한 번, 마지막으로 맞은 지 AlertRecent 초 안일 때만.
        private const double AlertInterval = 3.0;
        private const double AlertRecent = 1.0;

        // 앞길의 선체를 살피는 시간(초)과 선체 사이 여유
        private const float AvoidLookahead = 4.0F;
        private const float AvoidMargin = 5.0F;

        // 함재기가 찾은 적 대장선. 핑은 순회가 끝난 뒤에 보냄.
        private struct CraftSight
        {
            public Team Team;
            public float2 Position;
            public SpaceshipId Enemy;

            // 함재기가 모두 사라져 핑을 지움.
            public bool Clear;
        }

        private EntityQuery craftQuery;
        private EntityQuery hullQuery;

        public void OnCreate(ref SystemState state)
        {
            craftQuery = SystemAPI.QueryBuilder()
                .WithAll<CarriedCraftComponent, LocalTransform, SightOverlapElement>()
                .Build();
            hullQuery = SystemAPI.QueryBuilder()
                .WithAll<SpaceshipSpecComponent, ColliderComponent, LocalTransform>()
                .WithNone<CarriedCraftComponent>()
                .Build();

            state.RequireForUpdate<NetworkTime>();
            state.RequireForUpdate<GameModeComponent>();
            state.RequireForUpdate<KingCarrierConfigComponent>();
            state.RequireForUpdate<GameModeServiceComponent>();
            state.RequireForUpdate(SystemAPI.QueryBuilder().WithAll<KingCarrierComponent>().Build());
        }

        public void OnUpdate(ref SystemState state)
        {
            GameModeComponent mode = SystemAPI.GetSingleton<GameModeComponent>();
            KingCarrierConfigComponent config = SystemAPI.GetSingleton<KingCarrierConfigComponent>();
            GameModeService service = SystemAPI.ManagedAPI.GetSingleton<GameModeServiceComponent>().Value;

            NetworkTick tick = SystemAPI.GetSingleton<NetworkTime>().ServerTick;
            double time = SystemAPI.Time.ElapsedTime;
            float deltaTime = SystemAPI.Time.DeltaTime;

            // 지난 틱의 이동, 충돌 Job 이 쓰던 값을 읽음.
            state.CompleteDependency();

            using NativeArray<Entity> crafts = craftQuery.ToEntityArray(Allocator.Temp);
            using NativeArray<PathObstacle> obstacles = PathAvoidance.Collect(
                hullQuery,
                SystemAPI.GetComponentLookup<PlayerSpaceshipMovementComponent>(true),
                Allocator.Temp
            );

            // 팀 -> 대장선
            var kings = new NativeHashMap<int, Entity>(2, Allocator.Temp);

            foreach (var (king, entity) in SystemAPI.Query<RefRO<KingCarrierComponent>>().WithEntityAccess())
            {
                kings[(int)king.ValueRO.Team] = entity;
            }

            float2 boundary = math.max(mode.MapSize * 0.5F - BoundaryMargin, 0.0F);
            var sights = new NativeList<CraftSight>(2, Allocator.Temp);

            // 공격받는 기함의 팀. RPC 를 만드는 것은 구조 변경이라 순회가 끝난 뒤에 보냄.
            var alerts = new NativeList<Team>(2, Allocator.Temp);

            foreach (
                var (kingRef, transform, spec, carrier, movement, inputs, squads, entity) in
                SystemAPI.Query<
                    RefRW<KingCarrierComponent>,
                    RefRO<LocalTransform>,
                    RefRO<SpaceshipSpecComponent>,
                    RefRO<CarrierComponent>,
                    RefRO<MovementComponent>,
                    DynamicBuffer<InputBufferData<PlayerInputComponent>>,
                    DynamicBuffer<KingSquadElement>
                >().WithEntityAccess()
            ) {
                ref KingCarrierComponent king = ref kingRef.ValueRW;
                float2 position = transform.ValueRO.Position.xy;
                var random = Random.CreateFromIndex(math.hash(new uint2((uint)entity.Index, tick.SerializedData)) & 0x7FFFFFFFu);

                // 체력이 줄었으면 맞은 것.
                if (spec.ValueRO.Health < king.LastHealth)
                {
                    king.LastHitTime = time;
                }

                king.LastHealth = spec.ValueRO.Health;

                // 상대 팀에게 공격받는 중이면 팀원에게 경보를 보냄.
                if (king.LastEnemyHitTime > king.LastAlertTime
                    && time - king.LastEnemyHitTime < AlertRecent
                    && time - king.LastAlertTime >= AlertInterval)
                {
                    king.LastAlertTime = time;
                    alerts.Add(king.Team);
                }

                bool hasEnemy = kings.TryGetValue((int)Teams.Other(king.Team), out Entity enemy);
                float2 enemyPosition = hasEnemy ? SystemAPI.GetComponent<LocalTransform>(enemy).Position.xy : float2.zero;
                float enemyRadius = hasEnemy ? SystemAPI.GetComponent<SpaceshipSpecComponent>(enemy).Thickness * 0.5F : 0.0F;

                // 직접 찾음
                if (hasEnemy && math.distance(position, enemyPosition) <= config.DetectRange + enemyRadius)
                {
                    king.EnemyPosition = enemyPosition;
                    king.KnownUntil = math.max(king.KnownUntil, time + KingCarrierComponent.SightMemory);
                    king.EnemySighted = true;
                }

                // 함재기가 찾음. 팀원에게도 핑을 찍어 줌. 함재기가 모두 사라지면 핑을 지움.
                bool craftsOut = false;

                foreach (Entity craft in crafts)
                {
                    if (SystemAPI.GetComponent<CarriedCraftComponent>(craft).Carrier != entity)
                    {
                        continue;
                    }

                    craftsOut = true;

                    if (!hasEnemy)
                    {
                        continue;
                    }

                    foreach (SightOverlapElement sight in SystemAPI.GetBuffer<SightOverlapElement>(craft))
                    {
                        if (sight.Message.Other != enemy)
                        {
                            continue;
                        }

                        king.EnemyPosition = enemyPosition;
                        king.KnownUntil = math.max(king.KnownUntil, time + KingCarrierComponent.SightMemory);
                        sights.Add(new CraftSight
                        {
                            Team     = king.Team,
                            Position = enemyPosition,
                            Enemy    = SystemAPI.GetComponent<SpaceshipKindComponent>(enemy).Value,
                        });
                        break;
                    }
                }

                if (!craftsOut)
                {
                    sights.Add(new CraftSight { Team = king.Team, Clear = true });
                }

                bool known = time < king.KnownUntil;
                bool attacked = time - king.LastHitTime < config.ChasedTime;

                if (!known)
                {
                    king.EnemySighted = false;
                }

                KingCarrierState next = known ? KingCarrierState.Chasing
                    : attacked ? KingCarrierState.Fleeing
                    : KingCarrierState.Searching;

                // 도주는 한 방향으로만 달아남. 들어설 때 방향을 정함.
                if (next == KingCarrierState.Fleeing && king.State != KingCarrierState.Fleeing)
                {
                    king.Direction = random.NextFloat2Direction();
                }

                king.State = next;

                // 탐색 중에는 일정 시간마다 방향을 바꿈.
                king.CruiseRemaining -= deltaTime;

                if (king.State == KingCarrierState.Searching && king.CruiseRemaining <= 0.0F)
                {
                    king.Direction = random.NextFloat2Direction();
                    king.CruiseRemaining = random.NextFloat(config.CruiseTime.x, config.CruiseTime.y);
                }

                // 기수 앞이나 도착 예상 지점이 경계 밖이면 구역 안쪽 임의의 지점으로 돌아섬.
                // 이미 안쪽으로 돌아선 뒤에는 관성으로 밀려나는 동안 다시 고르지 않음.
                float2 ahead = position + king.Direction * BoundaryMargin;
                float2 predicted = position + movement.ValueRO.Velocity * PredictTime;
                float2 outward = math.select(float2.zero, math.sign(predicted), math.abs(predicted) > boundary);
                bool overshoot = math.any(outward != 0.0F);

                if (math.any(math.abs(ahead) > boundary) || (overshoot && math.dot(king.Direction, outward) > 0.0F))
                {
                    king.Direction = math.normalizesafe(random.NextFloat2(-boundary * 0.5F, boundary * 0.5F) - position, -king.Direction);
                    king.CruiseRemaining = random.NextFloat(config.CruiseTime.x, config.CruiseTime.y);
                }

                // 맵 이탈 방지. 매 틱 확인하고 다른 행동보다 먼저 함.
                float2 half = mode.MapSize * 0.5F;
                float2 velocity = movement.ValueRO.Velocity;
                float speed = math.length(velocity);
                float deceleration = math.max(SystemAPI.GetComponent<PlayerSpaceshipMovementComponent>(entity).Deceleration, 0.01F);
                float radius = spec.ValueRO.Thickness * 0.5F;
                float stopDistance = speed * speed / (2.0F * deceleration) + speed * ReturnReaction + radius;
                float2 stopPoint = position + math.normalizesafe(velocity) * stopDistance;

                bool outside = math.any(math.abs(position) > half);

                if (outside || math.any(math.abs(stopPoint) > math.max(half - EnterMargin, 0.0F)))
                {
                    king.Returning = true;
                }
                else if (king.Returning
                         && math.all(math.abs(position) <= math.max(half - ExitMargin, 0.0F))
                         && math.all(math.abs(stopPoint) <= math.max(half - ExitMargin, 0.0F)))
                {
                    king.Returning = false;
                }

                var input = new PlayerInputComponent();

                if (king.Returning)
                {
                    // 안쪽 상자에서 가장 가까운 점. 그쪽을 향해 줄여 가며 돌고, 느려지면 그쪽으로 미끄러짐.
                    float2 inner = math.max(half - InnerMargin, 0.0F);
                    float2 target = math.clamp(position, -inner, inner);
                    float2 toTarget = math.normalizesafe(target - position, math.normalizesafe(-position, -king.Direction));

                    king.Direction = toTarget;
                    king.CruiseRemaining = random.NextFloat(config.CruiseTime.x, config.CruiseTime.y);

                    input.MouseCursor = position + toTarget * CursorDistance;
                    input.AccelerationTriggered = true;
                    input.MoveArrows = toTarget;
                }
                else if (king.State == KingCarrierState.Chasing)
                {
                    // 적 대장선을 바라보며 유지 거리까지 다가가거나 물러남.
                    float keep = config.ChaseDistance;
                    float2 toEnemy = king.EnemyPosition - position;
                    float distance = math.length(toEnemy);

                    input.MouseCursor = king.EnemyPosition;
                    input.AccelerationTriggered = distance > keep + DistanceTolerance;

                    if (distance < keep - DistanceTolerance)
                    {
                        input.MoveArrows = -math.normalizesafe(toEnemy);
                    }

                    // 이대로면 구역 밖으로 나감. 밀고 나가지 않고 안쪽으로 되돌림.
                    if (overshoot)
                    {
                        input.AccelerationTriggered = false;
                        input.MoveArrows = math.normalizesafe(-outward);
                    }
                }
                else
                {
                    input.MouseCursor = position + king.Direction * CursorDistance;
                    input.AccelerationTriggered = true;
                }

                // 앞길을 선체가 막으면 비켜 감. 적 대장선은 추격 거리를 두므로 피하지 않음.
                float2 heading = math.normalizesafe(SpaceshipHeading.FromRotation(transform.ValueRO.Rotation), king.Direction);
                float2 toCursor = input.MouseCursor - position;

                if (math.lengthsq(toCursor) > math.EPSILON)
                {
                    float2 steered = PathAvoidance.Steer(
                        position,
                        math.normalizesafe(toCursor),
                        math.max(speed, 1.0F) * AvoidLookahead + stopDistance,
                        radius,
                        AvoidMargin,
                        obstacles,
                        entity,
                        hasEnemy ? enemy : Entity.Null,
                        false
                    );

                    input.MouseCursor = position + steered * math.max(math.length(toCursor), CursorDistance);
                }

                // 가려는 쪽이 기수에서 크게 벗어나 있으면 가속을 떼고 줄여 가며 돎. 느려지면 더 빨리 돎.
                float2 wanted = math.normalizesafe(input.MouseCursor - position, heading);

                if (input.AccelerationTriggered && math.dot(heading, wanted) < math.cos(math.radians(TurnAccelerationAngle)))
                {
                    input.AccelerationTriggered = false;

                    // 호버링 속력까지 줄면 가려는 쪽으로 미끄러짐.
                    if (math.lengthsq(input.MoveArrows) < math.EPSILON)
                    {
                        input.MoveArrows = wanted;
                    }
                }

                inputs.AddCommandData(new InputBufferData<PlayerInputComponent> { Tick = tick, InternalInput = input });

                // 편대 정리: 정찰대는 정찰 지점에 닿으면, 확보 편대는 지점에 닿았는데 교전할 적이 없으면,
                // 호위대는 더 쫓기지 않고 교전도 끝나면 돌아옴.
                for (int i = 0; i < squads.Length; i++)
                {
                    KingSquadElement squad = squads[i];

                    if (squad.Order == CarrierOrder.None || squad.Order == CarrierOrder.Return)
                    {
                        continue;
                    }

                    bool arrived = false;
                    bool engaging = false;

                    foreach (Entity craft in crafts)
                    {
                        CarriedCraftComponent craftState = SystemAPI.GetComponent<CarriedCraftComponent>(craft);

                        if (craftState.Carrier != entity || craftState.Squad != i)
                        {
                            continue;
                        }

                        // 교전하지 않는 함재기가 정찰 지점을 둘러싼 자기 자리에 닿으면 도착.
                        engaging |= craftState.Engaging;
                        arrived |= !craftState.Engaging
                                   && math.distance(SystemAPI.GetComponent<LocalTransform>(craft).Position.xy, craftState.Slot) <= config.ScoutArrival;
                    }

                    // 출격한 지 일정 시간이 지나면 무조건 돌아옴.
                    bool expired = time - squad.LaunchTime >= config.SquadTime;

                    if (expired
                        || (squad.Scouting && arrived)
                        || (squad.Order == CarrierOrder.SecureArea && arrived && !engaging)
                        || (squad.Order == CarrierOrder.Escort && squad.Pending == 0 && !engaging && king.State != KingCarrierState.Fleeing))
                    {
                        squad.Order = CarrierOrder.Return;
                        squad.Reassign = true;
                        squad.Scouting = false;
                        squads.ElementAt(i) = squad;
                    }
                }

                // 편대 출격. 최대 편대 수를 넘지 않음.
                king.LaunchRemaining -= deltaTime;

                if (king.LaunchRemaining > 0.0F || carrier.ValueRO.Stored == 0)
                {
                    continue;
                }

                int free = -1;

                for (int i = 0; i < squads.Length && free < 0; i++)
                {
                    if (squads[i].Order == CarrierOrder.None)
                    {
                        free = i;
                    }
                }

                if (free < 0)
                {
                    continue;
                }

                var launched = new KingSquadElement
                {
                    Reassign   = true,
                    LaunchTime = time,
                    Pending  = math.min(config.SquadSize, carrier.ValueRO.Stored),
                };

                switch (king.State)
                {
                    case KingCarrierState.Searching:
                        float2 scout = position + random.NextFloat2Direction() * random.NextFloat(config.ScoutDistance.x, config.ScoutDistance.y);

                        launched.Order = CarrierOrder.SecureArea;
                        launched.Position = math.clamp(scout, -mode.MapSize * 0.5F, mode.MapSize * 0.5F);
                        launched.Scouting = true;
                        break;

                    case KingCarrierState.Chasing:
                        launched.Order = CarrierOrder.SecureArea;
                        launched.Position = king.EnemyPosition;
                        break;

                    default:
                        launched.Order = CarrierOrder.Escort;
                        launched.Position = position;
                        break;
                }

                squads.ElementAt(free) = launched;
                king.LaunchRemaining = config.LaunchCooldown;
            }

            foreach (CraftSight sight in sights)
            {
                if (sight.Clear)
                {
                    service.ClearCraftSight(sight.Team);
                }
                else
                {
                    service.ReportCraftSight(sight.Team, sight.Position, sight.Enemy);
                }
            }

            foreach (Team team in alerts)
            {
                service.AlertFlagshipAttacked(team);
            }

            alerts.Dispose();
            sights.Dispose();
            kings.Dispose();
        }
    }
}
