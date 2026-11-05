using GalaxyIO.DOTS.Components.Actor;
using GalaxyIO.DOTS.Components.Singleton;
using GalaxyIO.DOTS.Networking;
using GalaxyIO.DOTS.Spaceship;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.NetCode;
using Unity.Transforms;
using Random = Unity.Mathematics.Random;

namespace GalaxyIO.DOTS.Bots
{
    /// <summary>
    /// 서버 봇 조종. 무리 진형 이동과 교전을 정하고 플레이어 입력(<see cref="PlayerInputComponent"/>)을
    /// 입력 버퍼에 직접 넣음. 이동, 사격은 플레이어와 같은 시스템이 처리함.
    /// <para/>
    /// 평소에는 무리 전체가 가장 느린 구성원의 속력(호버링 또는 비행)으로 리더를 따라 진형을 유지함.
    /// 교전 중에는 각자 성능대로 움직이고, 진형에서 멀어지면 가속해서 자리로 돌아옴.
    /// </summary>
    [WorldSystemFilter(WorldSystemFilterFlags.ServerSimulation)]
    [UpdateInGroup(typeof(SimulationSystemGroup), OrderFirst = true)]
    [UpdateAfter(typeof(GhostSimulationSystemGroup))]
    [UpdateBefore(typeof(PredictedSimulationSystemGroup))]
    [UpdateAfter(typeof(BotDirectorSystem))]
    public partial struct BotPilotSystem : ISystem
    {
        // 커서를 기수 앞 이 거리에 둠.
        private const float CursorDistance = 100.0F;

        // 가려는 방향이 기수에서 이 각도(도) 넘게 벗어나 있으면 가속하지 않고 돎.
        private const float TurnAccelerationAngle = 35.0F;

        // 대상을 다시 고르는 간격(초)
        private const float RetargetInterval = 0.5F;

        // 열이 이 비율 이상이면 식힘. 이 비율 이하로 식으면 다시 쏨.
        private const float HeatHigh = 0.85F;
        private const float HeatLow = 0.3F;

        // 진형 자리로 붙는 빠르기(1/초)
        private const float SlotGain = 0.8F;

        private const float AvoidLookahead = 3.0F;
        private const float AvoidMargin = 4.0F;

        private EntityQuery hullQuery;

        public void OnCreate(ref SystemState state)
        {
            hullQuery = SystemAPI.QueryBuilder()
                .WithAll<SpaceshipSpecComponent, ColliderComponent, LocalTransform, FactionComponent>()
                .WithNone<CarriedCraftComponent>()
                .Build();

            state.RequireForUpdate<NetworkTime>();
            state.RequireForUpdate<ItemRegistryElement>();
            state.RequireForUpdate(SystemAPI.QueryBuilder().WithAll<BotComponent>().Build());
        }

        public void OnUpdate(ref SystemState state)
        {
            BotSettings config = DedicatedServerConfig.Current?.Bots;

            if (config == null)
            {
                state.Enabled = false;
                return;
            }

            NetworkTick tick = SystemAPI.GetSingleton<NetworkTime>().ServerTick;
            float deltaTime = SystemAPI.Time.DeltaTime;

            // 지난 틱의 이동, 충돌 Job 이 쓰던 값을 읽음.
            state.CompleteDependency();

            using NativeArray<Entity> hulls = hullQuery.ToEntityArray(Allocator.Temp);
            using NativeArray<LocalTransform> hullTransforms = hullQuery.ToComponentDataArray<LocalTransform>(Allocator.Temp);
            using NativeArray<SpaceshipSpecComponent> hullSpecs = hullQuery.ToComponentDataArray<SpaceshipSpecComponent>(Allocator.Temp);
            using NativeArray<FactionComponent> hullFactions = hullQuery.ToComponentDataArray<FactionComponent>(Allocator.Temp);
            using NativeArray<PathObstacle> obstacles = PathAvoidance.Collect(
                hullQuery,
                SystemAPI.GetComponentLookup<PlayerSpaceshipMovementComponent>(true),
                Allocator.Temp
            );

            var hullIndex = new NativeHashMap<Entity, int>(hulls.Length, Allocator.Temp);

            // 플레이어 선체인지. 봇과 AI 적은 NetworkId 가 -1.
            ComponentLookup<GhostOwner> owners = SystemAPI.GetComponentLookup<GhostOwner>(true);
            var players = new NativeArray<bool>(hulls.Length, Allocator.Temp);

            for (int i = 0; i < hulls.Length; i++)
            {
                hullIndex[hulls[i]] = i;
                players[i] = owners.HasComponent(hulls[i]) && owners[hulls[i]].NetworkId > 0;
            }

            float playerScale = config.PlayerDetectScale > 0.0F ? config.PlayerDetectScale : 1.0F;
            float retargetInterval = config.RetargetInterval > 0.0F ? config.RetargetInterval : RetargetInterval;

            ComponentLookup<MovementComponent> movements = SystemAPI.GetComponentLookup<MovementComponent>(true);
            ComponentLookup<BotGroupComponent> groups = SystemAPI.GetComponentLookup<BotGroupComponent>();

            UpdateGroups(ref state, config, hulls, hullTransforms, hullSpecs, hullFactions, players, movements, ref groups, tick);

            DynamicBuffer<ItemRegistryElement> registry = SystemAPI.GetSingletonBuffer<ItemRegistryElement>(true);
            ComponentLookup<TurretHeatComponent> heats = SystemAPI.GetComponentLookup<TurretHeatComponent>(true);
            ComponentLookup<TurretSwitchComponent> switches = SystemAPI.GetComponentLookup<TurretSwitchComponent>(true);
            ComponentLookup<EquippedTurretComponent> equippedLookup = SystemAPI.GetComponentLookup<EquippedTurretComponent>(true);
            ComponentLookup<SpaceshipRadarComponent> radars = SystemAPI.GetComponentLookup<SpaceshipRadarComponent>(true);
            ComponentLookup<PlayerSpaceshipMovementComponent> pilots = SystemAPI.GetComponentLookup<PlayerSpaceshipMovementComponent>(true);
            ComponentLookup<CarrierComponent> carriers = SystemAPI.GetComponentLookup<CarrierComponent>();
            ComponentLookup<SpaceshipKindComponent> kinds = SystemAPI.GetComponentLookup<SpaceshipKindComponent>(true);

            foreach (var (botRef, transform, spec, faction, inputs, entity) in
                     SystemAPI.Query<
                         RefRW<BotComponent>,
                         RefRO<LocalTransform>,
                         RefRO<SpaceshipSpecComponent>,
                         RefRO<FactionComponent>,
                         DynamicBuffer<InputBufferData<PlayerInputComponent>>
                     >().WithEntityAccess())
            {
                ref BotComponent bot = ref botRef.ValueRW;

                if (spec.ValueRO.Health <= 0.0F || !groups.HasComponent(bot.Group))
                {
                    continue;
                }

                BotGroupComponent group = groups[bot.Group];
                BotGroupSettings settings = config.Groups[group.Index];
                var random = Random.CreateFromIndex(math.hash(new uint2((uint)entity.Index, tick.SerializedData)) & 0x7FFFFFFFu);

                float2 position = transform.ValueRO.Position.xy;
                float2 heading = math.normalizesafe(SpaceshipHeading.FromRotation(transform.ValueRO.Rotation), new float2(0.0F, 1.0F));
                float2 velocity = movements.HasComponent(entity) ? movements[entity].Velocity : float2.zero;
                float speed = math.length(velocity);
                PlayerSpaceshipMovementComponent pilot = pilots[entity];
                float ownRadius = spec.ValueRO.Thickness * 0.5F;
                float radar = radars.HasComponent(entity) ? radars[entity].Range : 80.0F;
                bool isCarrier = carriers.HasComponent(entity);

                // 지킬 기준점과 추격 한계. 기지는 제자리, 나머지는 무리 리더 기준.
                float2 anchor = bot.Behavior == BotBehavior.Base ? bot.Home : group.LeaderPosition;
                float leash = config.Leash;

                float detect = bot.Behavior switch
                {
                    BotBehavior.Sniper => math.max(bot.WeaponRange * 1.05F, radar),
                    BotBehavior.Rammer => radar * math.max(config.RammerDetectScale, 1.0F),
                    BotBehavior.Line => math.max(bot.WeaponRange * 0.9F, radar),
                    _ => math.max(radar, math.min(bot.WeaponRange, radar * 1.5F)),
                };

                if (settings.DetectRange > 0.0F)
                {
                    detect = settings.DetectRange;
                }

                // 찾기 규칙. 플레이어는 조금 가까이 와야 발견함(저격 제외). 크거나 센 무리는 플레이어 스폰 지역의 적을 노리지 않음.
                // 저격, 박치기는 멀리서 찾는 성질이라 플레이어 발견 거리 제한을 두지 않음.
                bool farSight = bot.Behavior == BotBehavior.Sniper || bot.Behavior == BotBehavior.Rammer;
                var keepRules = new TargetRules
                {
                    PlayerScale = 1.0F,
                    AvoidRadius = settings.AvoidSpawn ? math.max(config.SpawnAvoidRadius, 0.0F) : 0.0F,
                    WorldRadius = config.WorldRadius,
                };
                TargetRules findRules = keepRules;
                findRules.PlayerScale = farSight ? 1.0F : playerScale;
                findRules.PlayerReach = farSight ? 0.0F : BotRuntime.PlayerSight;

                // 피격: 체력이 줄었으면 근처의 가장 가까운 적을 공격자로 보고 무리 전체가 반격함(기지 제외).
                double now = SystemAPI.Time.ElapsedTime;
                bool damaged = bot.LastHealth > 0.0F && spec.ValueRO.Health < bot.LastHealth - 0.01F;
                bot.LastHealth = spec.ValueRO.Health;
                float giveUp = config.RetaliateRange * 1.8F;

                bool retaliating = bot.Behavior != BotBehavior.Base
                                   && group.RetaliateTarget != Entity.Null
                                   && now < group.RetaliateUntil
                                   && IsValidTarget(group.RetaliateTarget, faction.ValueRO, position, giveUp, position, float.MaxValue,
                                       keepRules, hullIndex, hullTransforms, hullSpecs, hullFactions);

                if (damaged && bot.Behavior != BotBehavior.Base && !retaliating)
                {
                    Entity attacker = FindNearest(entity, faction.ValueRO, position, config.RetaliateRange, position, float.MaxValue,
                        keepRules, players, hulls, hullTransforms, hullSpecs, hullFactions);

                    if (attacker != Entity.Null)
                    {
                        group.RetaliateTarget = attacker;
                        group.RetaliateUntil = now + config.RetaliateTime;
                        groups[bot.Group] = group;
                        retaliating = true;
                    }
                }

                // 대상 고르기. 반격 중이면 공격자, 전열은 무리가 함께 노림.
                if (retaliating)
                {
                    bot.Target = group.RetaliateTarget;
                }
                else if (bot.Behavior == BotBehavior.Line)
                {
                    bot.Target = group.Target;
                }
                else
                {
                    bot.RetargetRemaining -= deltaTime;

                    bool valid = IsValidTarget(bot.Target, faction.ValueRO, position, detect * 1.3F, anchor, leash,
                        keepRules, hullIndex, hullTransforms, hullSpecs, hullFactions);

                    if (!valid || bot.RetargetRemaining <= 0.0F)
                    {
                        bot.RetargetRemaining = retargetInterval;
                        Entity nearest = FindNearest(entity, faction.ValueRO, position, detect, anchor, leash,
                            findRules, players, hulls, hullTransforms, hullSpecs, hullFactions);

                        bot.Target = nearest != Entity.Null ? nearest : valid ? bot.Target : Entity.Null;
                    }
                }

                // 정찰 공유: 초계함이 잡은 적을 무리에 알림. 대상이 없는 구성원은 리더 기준 추격 한계 안이면 그 적을 노림.
                if (!retaliating && bot.Behavior != BotBehavior.Base)
                {
                    bool scout = kinds.HasComponent(entity) && IsCorvette(kinds[entity].Value);

                    if (scout && bot.Target != Entity.Null)
                    {
                        group.SharedTarget = bot.Target;
                        group.SharedUntil = now + 3.0;
                        groups[bot.Group] = group;
                    }
                    else if (bot.Target == Entity.Null
                             && group.SharedTarget != Entity.Null
                             && now < group.SharedUntil
                             && IsValidTarget(group.SharedTarget, faction.ValueRO, position, config.Leash * 2.0F, anchor, leash,
                                 keepRules, hullIndex, hullTransforms, hullSpecs, hullFactions))
                    {
                        bot.Target = group.SharedTarget;
                    }
                }

                // 맵 경계 밖: 교전을 끊고 진형(리더는 루트)으로 돌아옴. 박치기끼리 밀려 나가는 것도 여기서 멈춤.
                if (config.WorldRadius > 0.0F && math.lengthsq(position) > config.WorldRadius * config.WorldRadius)
                {
                    bot.Target = Entity.Null;
                    retaliating = false;
                }

                // 실수: 새 대상을 잡으면 잠깐 늦게 쏘고, 앞질러 겨누는 정도와 조준 각도가 조금씩 어긋남.
                // 저격은 장거리라 흔들림을 절반으로 둠(저격 성질 유지).
                float jitter = math.max(config.AimJitter, 0.0F) * (bot.Behavior == BotBehavior.Sniper ? 0.5F : 1.0F);

                if (bot.Target != bot.LastTarget)
                {
                    bot.LastTarget = bot.Target;
                    bot.ReactRemaining = random.NextFloat(math.min(config.ReactionMin, config.ReactionMax), math.max(config.ReactionMin, config.ReactionMax));
                    bot.AimLead = random.NextFloat(math.min(config.AimLeadMin, config.AimLeadMax), math.max(config.AimLeadMin, config.AimLeadMax));
                    bot.AimOffset = math.radians(random.NextFloat(-1.0F, 1.0F) * jitter);
                }
                else
                {
                    bot.ReactRemaining -= deltaTime;

                    // 가끔 조준 흔들림을 새로 굴림.
                    if (random.NextFloat() < deltaTime * 0.7F)
                    {
                        bot.AimOffset = math.radians(random.NextFloat(-1.0F, 1.0F) * jitter);
                    }
                }

                // 진형 자리
                float2 slot =group.LeaderPosition + group.LeaderVelocity * 0.5F
                              + BotRules.Rotate(bot.Offset - group.LeaderOffset, group.Heading);
                bool isLeader = entity == group.Leader;
                float rejoinDistance = math.max(40.0F, group.Spacing * 1.5F);
                float slotDistance = bot.Behavior == BotBehavior.Base ? math.distance(position, bot.Home)
                    : isLeader ? 0.0F : math.distance(position, slot);

                if (bot.Target != Entity.Null)
                {
                    bot.State = BotState.Engage;
                }
                else if (bot.State == BotState.Engage || slotDistance > rejoinDistance)
                {
                    bot.State = slotDistance > rejoinDistance * 0.4F ? BotState.Rejoin : BotState.Formation;
                }
                else if (bot.State == BotState.Rejoin && slotDistance <= rejoinDistance * 0.4F)
                {
                    bot.State = BotState.Formation;
                }

                // 전투 기록: 공격 중이거나 체력이 줄었으면 무리의 마지막 전투 시각을 갱신함(비전투 재편성 기준).
                if (bot.State == BotState.Engage || damaged)
                {
                    group.LastCombatTime = SystemAPI.Time.ElapsedTime;
                    groups[bot.Group] = group;
                }

                var input = new PlayerInputComponent
                {
                    MouseCursor = position + heading * CursorDistance,
                };

                bool trigger = false;
                float2 hoverDir = float2.zero;
                bool accelerate = false;

                if (bot.State == BotState.Engage && hullIndex.TryGetValue(bot.Target, out int targetIndex))
                {
                    float2 targetPosition = hullTransforms[targetIndex].Position.xy;
                    float2 targetVelocity = movements.HasComponent(bot.Target) ? movements[bot.Target].Velocity : float2.zero;
                    float targetRadius = hullSpecs[targetIndex].Thickness * 0.5F;
                    float distance = math.distance(position, targetPosition);

                    // 탄속으로 앞질러 겨눔. 앞지르는 정도와 각도에 실수가 섞임.
                    float2 aim = bot.ProjectileSpeed > 1.0F
                        ? targetPosition + targetVelocity * (distance / bot.ProjectileSpeed) * bot.AimLead
                        : targetPosition;

                    if (math.abs(bot.AimOffset) > 0.0001F)
                    {
                        float2 fromSelf = aim - position;
                        float sin = math.sin(bot.AimOffset);
                        float cos = math.cos(bot.AimOffset);
                        aim = position + new float2(fromSelf.x * cos - fromSelf.y * sin, fromSelf.x * sin + fromSelf.y * cos);
                    }

                    float2 toAim = math.normalizesafe(aim - position, heading);
                    float2 toTarget = math.normalizesafe(targetPosition - position, heading);
                    input.MouseCursor = aim;

                    float tolerance = math.clamp(math.atan2(targetRadius + 3.0F, math.max(distance, 1.0F)), math.radians(3.0F), math.radians(25.0F));
                    bool aligned = math.dot(heading, toAim) >= math.cos(tolerance);
                    float fireRange = bot.WeaponRange * 0.95F + targetRadius;
                    bool inRange = bot.WeaponRange > 0.0F && distance <= fireRange;

                    // 새 대상을 잡은 직후에는 아직 쏘지 않음.
                    trigger = bot.ReactRemaining <= 0.0F
                              && Trigger(ref bot, entity, inRange, aligned, registry, equippedLookup, heats, switches);

                    // 반격: 무기가 닿는 거리까지는 행동과 관계없이 최고 속도로 가속해 쫓아감. 닿으면 원래 행동대로 싸움.
                    bool chasing = retaliating
                                   && bot.Target == group.RetaliateTarget
                                   && distance > math.max(bot.WeaponRange * 0.85F, targetRadius + ownRadius + 10.0F);

                    if (chasing)
                    {
                        input.MouseCursor = aim;
                        accelerate = math.dot(heading, toTarget) > math.cos(math.radians(TurnAccelerationAngle));
                        hoverDir = accelerate ? float2.zero : toTarget;
                    }
                    else switch (bot.Behavior)
                    {
                        case BotBehavior.Sniper:
                        {
                            // 플레이어 레이더(복제 범위) 바깥, 사거리 끝에서 저격함.
                            float keep = math.max(bot.WeaponRange * 0.85F, radar);

                            if (distance > bot.WeaponRange * 1.3F)
                            {
                                accelerate = math.dot(heading, toTarget) > math.cos(math.radians(TurnAccelerationAngle));
                            }
                            else if (distance > bot.WeaponRange * 0.95F)
                            {
                                hoverDir = toTarget;
                            }
                            else if (distance < keep * 0.5F)
                            {
                                // 너무 가까움: 돌아서 달아남.
                                input.MouseCursor = position - toTarget * CursorDistance;
                                accelerate = math.dot(heading, -toTarget) > math.cos(math.radians(TurnAccelerationAngle));
                                trigger = false;
                            }
                            else if (distance < keep * 0.9F)
                            {
                                hoverDir = -toTarget;
                            }

                            break;
                        }

                        case BotBehavior.Rammer:
                            // 대상에 그대로 들이받음.
                            input.MouseCursor = aim;
                            accelerate = true;
                            break;

                        case BotBehavior.Line:
                            // 전열: 대열 자리를 지키며 호버링으로만 움직이고 대상을 향해 일제 사격.
                            if (!isLeader)
                            {
                                hoverDir = SlotDirection(position, slot, group.LeaderVelocity, pilot.HoveringSpeed, speed);
                            }
                            break;

                        case BotBehavior.Base:
                            hoverDir = HomeDirection(position, bot.Home, speed);
                            break;

                        default:
                        {
                            // 모선은 멀찍이 떨어져 함재기로 싸우고, 나머지는 사거리 안쪽에서 옆으로 돌며 쏨.
                            float keep = isCarrier
                                ? radar * 0.7F
                                : math.max(math.min(bot.WeaponRange * 0.6F, radar * 0.8F), targetRadius + ownRadius + 6.0F);

                            if (distance > keep * 1.3F)
                            {
                                accelerate = math.dot(heading, toTarget) > math.cos(math.radians(TurnAccelerationAngle));
                                hoverDir = accelerate ? float2.zero : toTarget;
                            }
                            else if (distance < keep * 0.7F)
                            {
                                hoverDir = -toTarget;
                            }
                            else
                            {
                                if (random.NextFloat() < 0.004F)
                                {
                                    bot.StrafeSign = -bot.StrafeSign;
                                }

                                hoverDir = new float2(-toTarget.y, toTarget.x) * bot.StrafeSign;
                            }

                            break;
                        }
                    }

                    // 모선은 함재기에 공격 명령. 박치기 무리는 어느 정도 가까워졌을 때만 꺼냄(그 전엔 넣어 둠).
                    if (isCarrier && bot.CraftsRam && distance > config.RammerLaunchRange)
                    {
                        CarrierComponent carrier = carriers[entity];

                        if (carrier.Order != CarrierOrder.Return && carrier.Order != CarrierOrder.None)
                        {
                            carrier.Order = CarrierOrder.Return;
                            carrier.Target = Entity.Null;
                            carrier.Position = position;
                            carrier.Reassign = true;
                            carriers[entity] = carrier;
                        }
                    }
                    else if (isCarrier)
                    {
                        CarrierComponent carrier = carriers[entity];

                        if (carrier.Order != CarrierOrder.Attack || carrier.Target != bot.Target)
                        {
                            carrier.Order = CarrierOrder.Attack;
                            carrier.Target = bot.Target;
                            carrier.Position = targetPosition;
                            carrier.Reassign = true;
                            carriers[entity] = carrier;
                        }
                    }
                }
                else
                {
                    bot.Target = Entity.Null;

                    // 박치기 무리는 전투가 끝나면 함재기를 모두 거둬들임. 다 들어올 때까지 움직이지 않음(아래).
                    bool recalling = false;

                    if (isCarrier)
                    {
                        CarrierComponent carrier = carriers[entity];
                        CarrierOrder idleOrder = bot.CraftsRam ? CarrierOrder.Return : CarrierOrder.Escort;

                        if (carrier.Order != idleOrder && !(bot.CraftsRam && carrier.Order == CarrierOrder.None))
                        {
                            carrier.Order = idleOrder;
                            carrier.Target = Entity.Null;
                            carrier.Position = position;
                            carrier.Reassign = true;
                            carriers[entity] = carrier;
                        }

                        recalling = bot.CraftsRam && carrier.Total > carrier.Stored;
                    }

                    if (recalling)
                    {
                        // 함재기가 다 들어올 때까지 제자리.
                        input.MouseCursor = position + heading * CursorDistance;
                        inputs.AddCommandData(new InputBufferData<PlayerInputComponent> { Tick = tick, InternalInput = input });
                        bot.LastTrigger = false;
                        continue;
                    }

                    if (bot.Behavior == BotBehavior.Base)
                    {
                        input.MouseCursor = position + bot.HomeHeading * CursorDistance;
                        hoverDir = HomeDirection(position, bot.Home, speed);
                    }
                    else if (isLeader)
                    {
                        float2 face = group.Heading;

                        if (!group.Waiting && group.HasWaypoint)
                        {
                            float2 toWaypoint = math.normalizesafe(group.Waypoint - position, group.Heading);
                            face = toWaypoint;

                            if (group.Fly)
                            {
                                accelerate = speed < group.CruiseSpeed
                                             && math.dot(heading, toWaypoint) > math.cos(math.radians(TurnAccelerationAngle));
                            }
                            else
                            {
                                hoverDir = speed < group.CruiseSpeed * 0.95F ? toWaypoint : float2.zero;
                            }
                        }

                        input.MouseCursor = position + face * CursorDistance;
                    }
                    else if (bot.State == BotState.Rejoin)
                    {
                        // 가속해서 자리로. 제동 거리 안에 들어오면 가속을 떼고 호버링으로 붙음.
                        float2 toSlot = math.normalizesafe(slot - position, heading);
                        float deceleration = math.max(pilot.Deceleration, 0.01F);
                        float stopDistance = speed * speed / (2.0F * deceleration);

                        input.MouseCursor = slot;
                        accelerate = stopDistance < slotDistance - group.Spacing
                                     && math.dot(heading, toSlot) > math.cos(math.radians(TurnAccelerationAngle));
                        hoverDir = accelerate ? float2.zero : toSlot;
                    }
                    else
                    {
                        // 진형 유지. 비행 무리는 리더가 빠르게 날 때 같이 가속하고, 대열을 맞출 때만 호버링함.
                        bool flyingFormation = group.Fly && math.length(group.LeaderVelocity) > pilot.HoveringSpeed * 0.9F;
                        float limit = flyingFormation ? pilot.MaxSpeed : pilot.HoveringSpeed;
                        float2 desired = group.LeaderVelocity + (slot - position) * SlotGain;
                        float desiredSpeed = math.min(math.length(desired), limit);
                        float2 desiredDir = math.normalizesafe(desired, group.Heading);

                        if (flyingFormation)
                        {
                            input.MouseCursor = position + desiredDir * CursorDistance;
                            accelerate = speed < desiredSpeed && math.dot(heading, desiredDir) > math.cos(math.radians(TurnAccelerationAngle));
                        }
                        else
                        {
                            input.MouseCursor = position + group.Heading * CursorDistance;
                            hoverDir = desiredSpeed > 0.3F && speed < desiredSpeed * 1.05F ? desiredDir : float2.zero;
                        }
                    }
                }

                // 앞길의 선체를 비켜 감. 박치기하는 대상은 피하지 않음.
                float2 toCursor = input.MouseCursor - position;

                if (accelerate && math.lengthsq(toCursor) > math.EPSILON)
                {
                    float2 steered = PathAvoidance.Steer(
                        position,
                        math.normalizesafe(toCursor),
                        math.max(speed, 1.0F) * AvoidLookahead + ownRadius * 2.0F,
                        ownRadius,
                        AvoidMargin,
                        obstacles,
                        entity,
                        bot.Target,
                        false
                    );

                    input.MouseCursor = position + steered * math.max(math.length(toCursor), CursorDistance);
                }

                if (math.lengthsq(hoverDir) > math.EPSILON)
                {
                    hoverDir = PathAvoidance.Steer(
                        position,
                        math.normalizesafe(hoverDir),
                        math.max(speed, 1.0F) * AvoidLookahead + ownRadius * 2.0F,
                        ownRadius,
                        AvoidMargin,
                        obstacles,
                        entity,
                        bot.Behavior == BotBehavior.Rammer ? bot.Target : Entity.Null,
                        false
                    );
                }

                input.AccelerationTriggered = accelerate;
                input.MoveArrows = hoverDir;
                input.TurretTriggered = trigger;
                input.TurretTriggeredInThisFrame = trigger && !bot.LastTrigger;
                bot.LastTrigger = trigger;

                inputs.AddCommandData(new InputBufferData<PlayerInputComponent> { Tick = tick, InternalInput = input });
            }

            hullIndex.Dispose();
            players.Dispose();
        }

        /// <summary>
        /// 무리마다 리더, 공동 속력, 순찰 지점, 진형 방향, 기다림 여부, 전열 대상을 정함.
        /// </summary>
        private void UpdateGroups(
            ref SystemState state,
            BotSettings config,
            NativeArray<Entity> hulls,
            NativeArray<LocalTransform> hullTransforms,
            NativeArray<SpaceshipSpecComponent> hullSpecs,
            NativeArray<FactionComponent> hullFactions,
            NativeArray<bool> players,
            ComponentLookup<MovementComponent> movements,
            ref ComponentLookup<BotGroupComponent> groups,
            NetworkTick tick)
        {
            float deltaTime = SystemAPI.Time.DeltaTime;

            // 무리 -> 리더 후보(가장 작은 진형 번호)
            var leaders = new NativeHashMap<Entity, Entity>(32, Allocator.Temp);
            var leaderSlots = new NativeHashMap<Entity, int>(32, Allocator.Temp);
            var cruise = new NativeHashMap<Entity, float>(32, Allocator.Temp);

            foreach (var (bot, spec, pilot, entity) in
                     SystemAPI.Query<RefRO<BotComponent>, RefRO<SpaceshipSpecComponent>, RefRO<PlayerSpaceshipMovementComponent>>()
                         .WithEntityAccess())
            {
                if (spec.ValueRO.Health <= 0.0F || !groups.HasComponent(bot.ValueRO.Group))
                {
                    continue;
                }

                Entity group = bot.ValueRO.Group;

                if (!leaderSlots.TryGetValue(group, out int best) || bot.ValueRO.Slot < best)
                {
                    leaderSlots[group] = bot.ValueRO.Slot;
                    leaders[group] = entity;
                }

                float own = groups[group].Fly ? pilot.ValueRO.MaxSpeed : pilot.ValueRO.HoveringSpeed;
                cruise[group] = cruise.TryGetValue(group, out float slowest) ? math.min(slowest, own) : own;
            }

            // 리더 Entity 를 찾아 위치를 기록함.
            foreach (var (bot, transform, entity) in SystemAPI.Query<RefRO<BotComponent>, RefRO<LocalTransform>>().WithEntityAccess())
            {
                Entity groupEntity = bot.ValueRO.Group;

                if (!leaders.TryGetValue(groupEntity, out Entity leader) || leader != entity)
                {
                    continue;
                }

                BotGroupComponent group = groups[groupEntity];
                group.Leader = entity;
                group.LeaderOffset = bot.ValueRO.Offset;
                group.LeaderPosition = transform.ValueRO.Position.xy;
                group.LeaderVelocity = movements.HasComponent(entity) ? movements[entity].Velocity : float2.zero;
                group.CruiseSpeed = cruise[groupEntity];
                groups[groupEntity] = group;
            }

            // 구성원이 진형에서 크게 벗어나 있으면 리더가 기다림.
            var waiting = new NativeHashSet<Entity>(16, Allocator.Temp);

            foreach (var (bot, transform) in SystemAPI.Query<RefRO<BotComponent>, RefRO<LocalTransform>>())
            {
                Entity groupEntity = bot.ValueRO.Group;

                if (!groups.HasComponent(groupEntity) || bot.ValueRO.State == BotState.Engage)
                {
                    continue;
                }

                BotGroupComponent group = groups[groupEntity];

                if (group.Leader == Entity.Null || group.Behavior == BotBehavior.Base)
                {
                    continue;
                }

                float2 slot = group.LeaderPosition + BotRules.Rotate(bot.ValueRO.Offset - group.LeaderOffset, group.Heading);

                if (math.distance(transform.ValueRO.Position.xy, slot) > group.Spacing * 3.0F + 40.0F)
                {
                    waiting.Add(groupEntity);
                }
            }

            foreach (var (groupRef, entity) in SystemAPI.Query<RefRW<BotGroupComponent>>().WithEntityAccess())
            {
                ref BotGroupComponent group = ref groupRef.ValueRW;

                // 앞의 루프가 lookup 으로 쓴 값을 다시 읽음.
                group = groups[entity];

                if (!group.Spawned || !leaders.ContainsKey(entity))
                {
                    group.Leader = Entity.Null;
                    continue;
                }

                group.Waiting = waiting.Contains(entity);

                var random = Random.CreateFromIndex(math.hash(new uint2((uint)entity.Index, tick.SerializedData)) & 0x7FFFFFFFu);

                DynamicBuffer<BotRoutePoint> route = SystemAPI.GetBuffer<BotRoutePoint>(entity);

                if (group.Behavior != BotBehavior.Base && route.Length >= 2)
                {
                    // 루트: 차례로 돌고 끝에서 처음으로. 닿으면 다음 점.
                    group.RouteIndex = math.clamp(group.RouteIndex, 0, route.Length - 1);

                    if (group.HasWaypoint && math.distance(group.LeaderPosition, group.Waypoint) < math.max(40.0F, group.Spacing * 2.0F))
                    {
                        group.RouteIndex = (group.RouteIndex + 1) % route.Length;
                    }

                    group.Waypoint = route[group.RouteIndex].Value;
                    group.HasWaypoint = true;

                    float2 toPoint = math.normalizesafe(group.Waypoint - group.LeaderPosition, group.Heading);
                    group.Heading = math.normalizesafe(
                        SpaceshipHeading.RotateTowards(math.normalizesafe(group.Heading, toPoint), toPoint, math.radians(25.0F) * deltaTime),
                        toPoint
                    );
                }
                else if (group.Behavior != BotBehavior.Base)
                {
                    float arrive = math.max(25.0F, group.Radius * 0.15F);

                    if (!group.HasWaypoint || math.distance(group.LeaderPosition, group.Waypoint) < arrive)
                    {
                        // 구역 안의 임의 지점. 너무 가까운 지점은 다시 뽑음.
                        float2 next = group.Center;

                        for (int i = 0; i < 8; i++)
                        {
                            next = group.Center + random.NextFloat2Direction() * math.sqrt(random.NextFloat()) * group.Radius;

                            if (math.distance(next, group.LeaderPosition) > group.Radius * 0.4F)
                            {
                                break;
                            }
                        }

                        group.Waypoint = next;
                        group.HasWaypoint = true;
                    }

                    // 진형 방향은 순찰 지점 쪽으로 천천히 돎.
                    float2 toWaypoint = math.normalizesafe(group.Waypoint - group.LeaderPosition, group.Heading);
                    group.Heading = math.normalizesafe(
                        SpaceshipHeading.RotateTowards(math.normalizesafe(group.Heading, toWaypoint), toWaypoint, math.radians(25.0F) * deltaTime),
                        toWaypoint
                    );
                }

                // 전열: 리더 기준 가장 가까운 적을 무리가 함께 노림.
                if (group.Behavior == BotBehavior.Line && hullIndexOf(hulls, group.Leader, out int leaderHull))
                {
                    float range = 0.0F;

                    if (SystemAPI.HasComponent<BotComponent>(group.Leader))
                    {
                        range = SystemAPI.GetComponent<BotComponent>(group.Leader).WeaponRange * 0.9F;
                    }

                    float radar = SystemAPI.HasComponent<SpaceshipRadarComponent>(group.Leader)
                        ? SystemAPI.GetComponent<SpaceshipRadarComponent>(group.Leader).Range
                        : 80.0F;

                    float detectRange = config.Groups[group.Index].DetectRange > 0.0F
                        ? config.Groups[group.Index].DetectRange
                        : math.max(range, radar);

                    BotGroupSettings lineSettings = config.Groups[group.Index];
                    var lineRules = new TargetRules
                    {
                        PlayerScale = config.PlayerDetectScale > 0.0F ? config.PlayerDetectScale : 1.0F,
                        PlayerReach = BotRuntime.PlayerSight,
                        AvoidRadius = lineSettings.AvoidSpawn ? math.max(config.SpawnAvoidRadius, 0.0F) : 0.0F,
                        WorldRadius = config.WorldRadius,
                    };

                    group.Target = FindNearest(group.Leader, hullFactions[leaderHull], group.LeaderPosition, detectRange,
                        group.LeaderPosition, config.Leash, lineRules, players, hulls, hullTransforms, hullSpecs, hullFactions);
                }
            }

            waiting.Dispose();
            cruise.Dispose();
            leaderSlots.Dispose();
            leaders.Dispose();
        }

        private static bool hullIndexOf(NativeArray<Entity> hulls, Entity entity, out int index)
        {
            for (int i = 0; i < hulls.Length; i++)
            {
                if (hulls[i] == entity)
                {
                    index = i;
                    return true;
                }
            }

            index = -1;
            return false;
        }

        /// <summary>
        /// 방아쇠. 열이 높으면 식힘. 충전 포탑은 사거리 안이면 충전하고, 다 찼고 겨눴으면 떼서 쏨.
        /// </summary>
        private static bool Trigger(
            ref BotComponent bot,
            Entity entity,
            bool inRange,
            bool aligned,
            DynamicBuffer<ItemRegistryElement> registry,
            ComponentLookup<EquippedTurretComponent> equippedLookup,
            ComponentLookup<TurretHeatComponent> heats,
            ComponentLookup<TurretSwitchComponent> switches)
        {
            if (!equippedLookup.HasComponent(entity) || bot.WeaponRange <= 0.0F)
            {
                return false;
            }

            if (heats.HasComponent(entity))
            {
                TurretHeatComponent heat = heats[entity];

                if (heat.MaxHeat > 0.0F)
                {
                    if (heat.Heat >= heat.MaxHeat * HeatHigh)
                    {
                        bot.Cooling = true;
                    }
                    else if (bot.Cooling && heat.Heat <= heat.MaxHeat * HeatLow)
                    {
                        bot.Cooling = false;
                    }
                }

                if (heat.Overheated || bot.Cooling)
                {
                    return false;
                }
            }

            ItemRegistryElement turret = registry.Get(equippedLookup[entity].Value);

            if (turret.ReleaseFire && switches.HasComponent(entity))
            {
                TurretSwitchComponent turretSwitch = switches[entity];
                bool charged = turretSwitch.State == TurretSwitchState.SpinningUp && turretSwitch.Remaining <= 0.0F;

                // 다 찼고 겨눴으면 떼서 쏨. 아니면 사거리 안에서 계속 충전함.
                return inRange && !(charged && aligned);
            }

            return inRange && aligned;
        }

        private static bool IsCorvette(SpaceshipId kind)
        {
            return kind == SpaceshipId.Vex || kind == SpaceshipId.Vulture || kind == SpaceshipId.Nighthawk;
        }

        private static float2 HomeDirection(float2 position, float2 home, float speed)
        {
            float2 offset = home - position;
            float distance = math.length(offset);

            // 가까우면 멈추고, 멀어지면 천천히 돌아감.
            return distance > 3.0F && speed < math.min(distance, 6.0F) ? offset / distance : float2.zero;
        }

        private static float2 SlotDirection(float2 position, float2 slot, float2 leaderVelocity, float hoverSpeed, float speed)
        {
            float2 desired = leaderVelocity + (slot - position) * SlotGain;
            float desiredSpeed = math.min(math.length(desired), hoverSpeed);

            return desiredSpeed > 0.3F && speed < desiredSpeed * 1.05F ? math.normalizesafe(desired) : float2.zero;
        }

        private static bool IsHostile(in FactionComponent self, in FactionComponent other)
        {
            return other.Value != FactionID.None && !Faction.IsFriendly(self, other);
        }

        /// <summary>
        /// 대상 찾기 규칙. PlayerScale: 플레이어를 발견하는 거리 배율. AvoidRadius: 0 보다 크면 원점(플레이어 스폰) 이 반경 안의 적은 제외.
        /// </summary>
        private struct TargetRules
        {
            public float PlayerScale;

            // 플레이어 발견 거리 상한(0 = 없음). 초계함 화면 시야 기준.
            public float PlayerReach;

            public float AvoidRadius;

            // 맵 경계(0 = 없음). 그 밖의 적은 노리지 않음.
            public float WorldRadius;

            public bool InAvoidZone(float2 point)
            {
                return (AvoidRadius > 0.0F && math.lengthsq(point) < AvoidRadius * AvoidRadius)
                       || (WorldRadius > 0.0F && math.lengthsq(point) > WorldRadius * WorldRadius);
            }

            public float Reach(float range, bool player)
            {
                if (!player)
                {
                    return range;
                }

                float reach = range * PlayerScale;
                return PlayerReach > 0.0F ? math.min(reach, PlayerReach) : reach;
            }
        }

        private static bool IsValidTarget(
            Entity target,
            in FactionComponent self,
            float2 position,
            float range,
            float2 anchor,
            float leash,
            in TargetRules rules,
            NativeHashMap<Entity, int> hullIndex,
            NativeArray<LocalTransform> transforms,
            NativeArray<SpaceshipSpecComponent> specs,
            NativeArray<FactionComponent> factions)
        {
            if (target == Entity.Null || !hullIndex.TryGetValue(target, out int i))
            {
                return false;
            }

            float2 other = transforms[i].Position.xy;

            return specs[i].Health > 0.0F
                   && IsHostile(self, factions[i])
                   && !rules.InAvoidZone(other)
                   && math.distance(position, other) <= range + specs[i].Thickness * 0.5F
                   && math.distance(anchor, other) <= leash;
        }

        private static Entity FindNearest(
            Entity self,
            in FactionComponent faction,
            float2 position,
            float range,
            float2 anchor,
            float leash,
            in TargetRules rules,
            NativeArray<bool> players,
            NativeArray<Entity> hulls,
            NativeArray<LocalTransform> transforms,
            NativeArray<SpaceshipSpecComponent> specs,
            NativeArray<FactionComponent> factions)
        {
            Entity best = Entity.Null;
            float bestDistance = float.MaxValue;

            for (int i = 0; i < hulls.Length; i++)
            {
                if (hulls[i] == self || specs[i].Health <= 0.0F || !IsHostile(faction, factions[i]))
                {
                    continue;
                }

                float2 other = transforms[i].Position.xy;
                float distance = math.distance(position, other) - specs[i].Thickness * 0.5F;
                float reach = rules.Reach(range, players[i]);

                if (distance > reach || distance >= bestDistance || math.distance(anchor, other) > leash || rules.InAvoidZone(other))
                {
                    continue;
                }

                best = hulls[i];
                bestDistance = distance;
            }

            return best;
        }
    }
}
