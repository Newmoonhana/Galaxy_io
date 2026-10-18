using GalaxyIO.DOTS.Collision;
using GalaxyIO.DOTS.Components.Actor;
using GalaxyIO.DOTS.Components.Singleton;
using GalaxyIO.DOTS.Inventory;
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
    /// 모선의 함재기 생산, 출격, 흡수와 명령 유지를 담당하는 서버 시스템. 흡수한 함재기의 화물은 주인 인벤토리로 옮김.
    /// 함재기마다 이번 틱의 명령, 교전 대상, 진형 중심, 진형 자리를 채우고, 조종은 <see cref="CarriedCraftBehaviorSystem"/>이 함.
    /// 교전 중인 함재기는 대상을 둘러싼 고리 위를 돎. 지역 확보와 호위는 함재기마다 같은 모선의 다른 함재기가 덜 노리는 적을 고름.
    /// </summary>
    [WorldSystemFilter(WorldSystemFilterFlags.ServerSimulation)]
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateAfter(typeof(CarrierOrderSystem))]
    public partial struct CarrierSystem : ISystem
    {
        // 강화로 줄일 수 있는 가장 짧은 생산 시간(초)
        private const float MinAssemblyTime = 0.5F;

        // 모선이 사라진 뒤 함재기가 터지기까지의 시간 범위(초). 함재기마다 뽑아 한꺼번에 터지지 않게 함.
        private const float MinExplodeDelay = 0.2F;
        private const float MaxExplodeDelay = 0.8F;

        private EntityQuery craftQuery;

        public void OnCreate(ref SystemState state)
        {
            craftQuery = SystemAPI.QueryBuilder().WithAll<CarriedCraftComponent, LocalTransform, SpaceshipSpecComponent>().Build();

            state.RequireForUpdate<NetworkTime>();
            state.RequireForUpdate<InventoryServiceComponent>();
            state.RequireAnyForUpdate(SystemAPI.QueryBuilder().WithAll<CarrierComponent>().Build(), craftQuery);
        }

        public void OnUpdate(ref SystemState state)
        {
            float deltaTime = SystemAPI.Time.DeltaTime;
            uint tick = SystemAPI.GetSingleton<NetworkTime>().ServerTick.SerializedData;
            InventoryService inventories = SystemAPI.ManagedAPI.GetSingleton<InventoryServiceComponent>().Value;

            var ecb = new EntityCommandBuffer(Allocator.Temp);

            using NativeArray<Entity> crafts = craftQuery.ToEntityArray(Allocator.Temp);

            // 모선이 사라지면(격추, 기종 변경) 함재기도 잠시 뒤 터짐.
            foreach (Entity craft in crafts)
            {
                CarriedCraftComponent craftState = SystemAPI.GetComponent<CarriedCraftComponent>(craft);

                if (SystemAPI.HasComponent<CarrierComponent>(craftState.Carrier))
                {
                    continue;
                }

                if (!craftState.Abandoned)
                {
                    var random = Random.CreateFromIndex(math.hash(new uint2((uint)craft.Index, tick)) & 0x7FFFFFFFu);

                    craftState.Abandoned = true;
                    craftState.ExplodeRemaining = random.NextFloat(MinExplodeDelay, MaxExplodeDelay);
                }
                else
                {
                    craftState.ExplodeRemaining -= deltaTime;
                }

                // 체력이 0 이면 SpaceshipDeathSystem 이 폭발 연출과 함께 지움. 그 전에 수리되지 않게 막음.
                if (craftState.ExplodeRemaining <= 0.0F)
                {
                    SpaceshipSpecComponent craftHealth = SystemAPI.GetComponent<SpaceshipSpecComponent>(craft);
                    craftHealth.Health = 0.0F;
                    craftHealth.RepairCooldown = craftHealth.RepairDelay;
                    SystemAPI.SetComponent(craft, craftHealth);
                }

                SystemAPI.SetComponent(craft, craftState);
            }

            // NetworkId -> 레이더 탐지 거리. 복제 반경과 같음.
            var ranges = new NativeHashMap<int, float>(8, Allocator.Temp);

            foreach (var (networkId, radius) in SystemAPI.Query<RefRO<NetworkId>, RefRO<RelevancyRadiusComponent>>())
            {
                ranges[networkId.ValueRO.Value] = radius.ValueRO.Value;
            }

            var hulls = new Hulls
            {
                Transforms = SystemAPI.GetComponentLookup<LocalTransform>(true),
                Colliders  = SystemAPI.GetComponentLookup<ColliderComponent>(true),
                Specs      = SystemAPI.GetComponentLookup<SpaceshipSpecComponent>(true),
                Movements  = SystemAPI.GetComponentLookup<SpaceshipMovementComponent>(true),
            };

            // 모선마다 비워서 다시 씀.
            var mine = new NativeList<Entity>(8, Allocator.Temp);
            var states = new NativeList<CarriedCraftComponent>(8, Allocator.Temp);
            var positions = new NativeList<float2>(8, Allocator.Temp);
            var absorbed = new NativeList<bool>(8, Allocator.Temp);

            // 적 -> 그 적을 노리는 같은 모선의 함재기 수
            var engaged = new NativeHashMap<Entity, int>(8, Allocator.Temp);

            foreach (
                var (carrierRef, transform, spec, movement, faction, owner, entity) in
                SystemAPI.Query<
                    RefRW<CarrierComponent>,
                    RefRO<LocalTransform>,
                    RefRO<SpaceshipSpecComponent>,
                    RefRO<SpaceshipMovementComponent>,
                    RefRO<FactionComponent>,
                    RefRO<GhostOwner>
                >().WithEntityAccess()
            ) {
                ref CarrierComponent carrier = ref carrierRef.ValueRW;
                CarriedCraftSpecComponent craftSpec = SystemAPI.GetComponent<CarriedCraftSpecComponent>(carrier.CraftPrefab);

                float2 position = transform.ValueRO.Position.xy;
                float2 velocity = movement.ValueRO.DeltaMovement;
                float2 heading = SpaceshipHeading.FromRotation(transform.ValueRO.Rotation);
                float range = ranges.TryGetValue(owner.ValueRO.NetworkId, out float radarRange) ? radarRange : float.MaxValue;

                var random = Random.CreateFromIndex(math.hash(new uint2((uint)entity.Index, tick)) & 0x7FFFFFFFu);

                carrier.OrderRemaining = math.max(carrier.OrderRemaining - deltaTime, 0.0F);

                mine.Clear();
                states.Clear();
                positions.Clear();
                absorbed.Clear();

                foreach (Entity craft in crafts)
                {
                    CarriedCraftComponent craftState = SystemAPI.GetComponent<CarriedCraftComponent>(craft);

                    if (craftState.Carrier == entity)
                    {
                        mine.Add(craft);
                        states.Add(craftState);
                        positions.Add(SystemAPI.GetComponent<LocalTransform>(craft).Position.xy);
                        absorbed.Add(false);
                    }
                }

                // 명령 지점과 그 이동 속도, 지역 확보 / 호위 진형 첫 고리 반지름. 공격 대상의 선체를 감싸는 반지름.
                float2 anchorVelocity = float2.zero;
                float firstRadius = craftSpec.SecureRadius;
                float attackHull = 0.0F;

                switch (carrier.Order)
                {
                    case CarrierOrder.Attack:
                        // 대상이 사라지거나 격추되면 돌아옴.
                        if (hulls.TryGet(carrier.Target, out float2 attackCenter, out attackHull, out anchorVelocity))
                        {
                            carrier.Position = attackCenter;
                        }
                        else
                        {
                            carrier.Order = CarrierOrder.Return;
                            carrier.Reassign = true;
                        }
                        break;

                    case CarrierOrder.Escort:
                        carrier.Position = position;
                        anchorVelocity = velocity;
                        firstRadius = spec.ValueRO.Thickness * 0.5F + craftSpec.EscortRadius;
                        break;
                }

                // 명령 지점이 레이더 밖으로 나가면 돌아옴.
                if ((carrier.Order == CarrierOrder.Attack || carrier.Order == CarrierOrder.SecureArea)
                    && math.distancesq(position, carrier.Position) > range * range)
                {
                    carrier.Order = CarrierOrder.Return;
                    carrier.Reassign = true;
                }

                if (carrier.Order == CarrierOrder.Return)
                {
                    carrier.Target = Entity.Null;

                    for (int i = 0; i < states.Length; i++)
                    {
                        CarriedCraftComponent craftState = states[i];

                        if (craftState.Returning)
                        {
                            continue;
                        }

                        // 함재기마다 출발 시각을 다르게 해서 서로 덜 부딪힘.
                        if (carrier.Reassign)
                        {
                            craftState.ReturnRemaining = random.NextFloat(0.0F, carrier.ReturnDelay);
                        }

                        craftState.ReturnRemaining -= deltaTime;
                        craftState.Returning = craftState.ReturnRemaining <= 0.0F;
                        states[i] = craftState;
                    }

                    carrier.Reassign = false;
                }

                // 공격 중 체력이 낮은 함재기는 돌아가 흡수됨. 수리된 채로 다시 나감.
                if (carrier.Order == CarrierOrder.Attack)
                {
                    for (int i = 0; i < states.Length; i++)
                    {
                        SpaceshipSpecComponent craftHealth = SystemAPI.GetComponent<SpaceshipSpecComponent>(mine[i]);

                        if (craftHealth.Health < craftHealth.MaxHealth * craftSpec.RetreatHealth)
                        {
                            CarriedCraftComponent craftState = states[i];
                            craftState.Returning = true;
                            states[i] = craftState;
                        }
                    }
                }

                // 모선에 닿은 귀환 함재기를 흡수함.
                float absorbRadius = spec.ValueRO.Thickness * 0.5F;
                int outside = 0;

                for (int i = 0; i < states.Length; i++)
                {
                    if (states[i].Returning && math.distancesq(positions[i], position) <= absorbRadius * absorbRadius)
                    {
                        absorbed[i] = true;
                        ecb.DestroyEntity(mine[i]);
                        carrier.Stored++;

                        foreach (CarriedItemElement carried in SystemAPI.GetBuffer<CarriedItemElement>(mine[i]))
                        {
                            inventories.Insert(owner.ValueRO.NetworkId, carried.Id, carried.Count);
                        }

                        continue;
                    }

                    outside++;
                }

                if (carrier.Order == CarrierOrder.Return && outside == 0)
                {
                    carrier.Order = CarrierOrder.None;
                }

                // 강화 수치. 강화가 없는 기체는 기본값.
                float assemblyBonus = 0.0F;
                float damageScale = 1.0F;
                float healthScale = 1.0F;

                if (SystemAPI.HasBuffer<UpgradeParamElement>(entity))
                {
                    DynamicBuffer<UpgradeParamElement> parameters = SystemAPI.GetBuffer<UpgradeParamElement>(entity);
                    DynamicBuffer<UpgradeLevelElement> levels = SystemAPI.GetBuffer<UpgradeLevelElement>(entity);

                    assemblyBonus = Upgrade.Value(parameters, levels, UpgradeParam.AssemblyTime);
                    damageScale = Upgrade.Value(parameters, levels, UpgradeParam.CraftDamage);
                    healthScale = Upgrade.Value(parameters, levels, UpgradeParam.CraftMaxHealth);
                }

                // 최대 수보다 적으면 만듦.
                float assemblyTime = math.max(carrier.AssemblyTime - assemblyBonus, MinAssemblyTime);

                if (carrier.Stored + outside < carrier.Capacity)
                {
                    carrier.AssemblyRemaining -= deltaTime;

                    if (carrier.AssemblyRemaining <= 0.0F)
                    {
                        carrier.Stored++;
                        carrier.AssemblyRemaining = assemblyTime;
                    }
                }
                else
                {
                    carrier.AssemblyRemaining = assemblyTime;
                }

                carrier.Total = (byte)(carrier.Stored + outside);
                carrier.AssemblyProgress = carrier.Total < carrier.Capacity
                    ? math.saturate(1.0F - carrier.AssemblyRemaining / assemblyTime)
                    : 1.0F;

                bool deploying = carrier.Order == CarrierOrder.Attack
                                 || carrier.Order == CarrierOrder.SecureArea
                                 || carrier.Order == CarrierOrder.Escort;

                // 진형에 설 함재기 수. 모선 안의 함재기는 나오는 대로 빈 자리에 섬.
                int active = 0;

                for (int i = 0; i < states.Length; i++)
                {
                    if (!absorbed[i] && !states[i].Returning)
                    {
                        active++;
                    }
                }

                int formationCount = active + carrier.Stored;

                if (deploying && carrier.Order == CarrierOrder.Attack)
                {
                    // 새 명령이면 대상을 둘러싼 무작위 자리를 뽑고 이동 거리 합이 가장 짧게 나눔.
                    if (carrier.Reassign && active > 0)
                    {
                        var rings = new NativeArray<int>(active, Allocator.Temp);
                        var angles = new NativeArray<float>(active, Allocator.Temp);
                        var cost = new NativeArray<float>(active * active, Allocator.Temp);
                        var assignment = new NativeArray<int>(active, Allocator.Temp);

                        for (int j = 0; j < active; j++)
                        {
                            rings[j] = random.NextInt(0, craftSpec.AttackRings);
                            angles[j] = random.NextFloat(0.0F, 2.0F * math.PI);
                        }

                        for (int i = 0, row = 0; i < states.Length; i++)
                        {
                            if (absorbed[i] || states[i].Returning)
                            {
                                continue;
                            }

                            for (int j = 0; j < active; j++)
                            {
                                float2 slot = carrier.Position + CarrierFormation.Direction(angles[j])
                                              * CarrierFormation.RingRadius(attackHull + craftSpec.AttackRadius, craftSpec.RingGap, rings[j]);
                                cost[row * active + j] = math.distance(positions[i], slot);
                            }

                            row++;
                        }

                        CarrierFormation.Assign(cost, active, active, assignment);

                        for (int i = 0, row = 0; i < states.Length; i++)
                        {
                            if (absorbed[i] || states[i].Returning)
                            {
                                continue;
                            }

                            CarriedCraftComponent craftState = states[i];
                            craftState.Target = carrier.Target;
                            craftState.Engaging = true;
                            craftState.Approaching = true;
                            craftState.Ring = rings[assignment[row]];
                            craftState.Angle = angles[assignment[row]];
                            craftState.Orbit = random.NextBool() ? 1.0F : -1.0F;
                            craftState.DodgeRemaining = random.NextFloat(craftSpec.DodgeInterval.x, craftSpec.DodgeInterval.y);
                            states[i] = craftState;
                            row++;
                        }

                        rings.Dispose();
                        angles.Dispose();
                        cost.Dispose();
                        assignment.Dispose();
                    }

                    carrier.Reassign = false;
                }
                else if (deploying)
                {
                    if (carrier.Reassign)
                    {
                        // 새 명령이면 하던 교전을 그만두고 대상을 새로 고름.
                        for (int i = 0; i < states.Length; i++)
                        {
                            CarriedCraftComponent craftState = states[i];
                            craftState.Target = Entity.Null;
                            craftState.Engaging = false;
                            craftState.Ring = -1;
                            craftState.RetargetRemaining = 0.0F;
                            states[i] = craftState;
                        }
                    }

                    // 새 명령이거나 진형에 설 수가 바뀌면 자리를 다시 나눔. 이동 거리 합이 가장 짧게 나눔.
                    if ((carrier.Reassign || carrier.FormationCount != formationCount) && active > 0)
                    {
                        var cost = new NativeArray<float>(active * formationCount, Allocator.Temp);
                        var assignment = new NativeArray<int>(active, Allocator.Temp);

                        for (int i = 0, row = 0; i < states.Length; i++)
                        {
                            if (absorbed[i] || states[i].Returning)
                            {
                                continue;
                            }

                            for (int j = 0; j < formationCount; j++)
                            {
                                float2 slot = carrier.Position
                                              + CarrierFormation.RingSlot(j, formationCount, firstRadius, craftSpec.RingGap, craftSpec.SlotGap);
                                cost[row * formationCount + j] = math.distance(positions[i], slot);
                            }

                            row++;
                        }

                        CarrierFormation.Assign(cost, active, formationCount, assignment);

                        for (int i = 0, row = 0; i < states.Length; i++)
                        {
                            if (absorbed[i] || states[i].Returning)
                            {
                                continue;
                            }

                            CarriedCraftComponent craftState = states[i];
                            craftState.SlotIndex = assignment[row++];
                            states[i] = craftState;
                        }

                        cost.Dispose();
                        assignment.Dispose();
                    }

                    carrier.FormationCount = formationCount;
                    carrier.Reassign = false;

                    // 교전하지 않는 함재기가 설 진형 자리
                    for (int i = 0; i < states.Length; i++)
                    {
                        CarriedCraftComponent craftState = states[i];

                        if (absorbed[i] || craftState.Returning || craftState.SlotIndex < 0)
                        {
                            continue;
                        }

                        craftState.Slot = carrier.Position
                                          + CarrierFormation.RingSlot(craftState.SlotIndex, formationCount, firstRadius, craftSpec.RingGap, craftSpec.SlotGap);
                        states[i] = craftState;
                    }

                    // 적마다 노리는 함재기 수
                    engaged.Clear();

                    for (int i = 0; i < states.Length; i++)
                    {
                        if (!absorbed[i] && !states[i].Returning && states[i].Engaging)
                        {
                            engaged[states[i].Target] = (engaged.TryGetValue(states[i].Target, out int count) ? count : 0) + 1;
                        }
                    }

                    // 대상이 사라지거나 멀어지면 바로, 아니면 잠시 뒤 다시 고름.
                    // 가까울수록, 다른 함재기가 덜 노릴수록 좋은 대상. 지금 대상은 조금 더 쳐줘서 쉽게 바꾸지 않음.
                    for (int i = 0; i < states.Length; i++)
                    {
                        CarriedCraftComponent craftState = states[i];

                        if (absorbed[i] || craftState.Returning || craftState.WindUpRemaining > 0.0F)
                        {
                            continue;
                        }

                        craftState.RetargetRemaining -= deltaTime;

                        float chaseDistance = SystemAPI.GetComponent<EnemyPerceptionComponent>(mine[i]).ChaseDistance;
                        Entity best = Entity.Null;
                        float bestScore = float.MaxValue;

                        if (craftState.Engaging
                            && hulls.TryGet(craftState.Target, out float2 currentCenter, out float currentHull, out _)
                            && math.distance(positions[i], currentCenter) - currentHull <= chaseDistance
                            && (carrier.Order != CarrierOrder.Escort || math.distance(currentCenter, position) <= craftSpec.LeashRadius))
                        {
                            if (craftState.RetargetRemaining > 0.0F)
                            {
                                states[i] = craftState;
                                continue;
                            }

                            best = craftState.Target;
                            bestScore = math.distance(positions[i], currentCenter) - currentHull
                                        + craftSpec.SpreadDistance * (engaged[craftState.Target] - 1.5F);
                        }

                        foreach (SightOverlapElement sight in SystemAPI.GetBuffer<SightOverlapElement>(mine[i]))
                        {
                            Entity candidate = sight.Message.Other;

                            if (candidate == craftState.Target
                                || !hulls.TryGet(candidate, out float2 center, out float hull, out _)
                                || (carrier.Order == CarrierOrder.Escort && math.distance(center, position) > craftSpec.LeashRadius))
                            {
                                continue;
                            }

                            float score = math.distance(positions[i], center) - hull
                                          + craftSpec.SpreadDistance * (engaged.TryGetValue(candidate, out int others) ? others : 0);

                            if (score < bestScore)
                            {
                                best = candidate;
                                bestScore = score;
                            }
                        }

                        craftState.RetargetRemaining = craftSpec.RetargetCooldown;

                        if (best != craftState.Target || !craftState.Engaging)
                        {
                            if (craftState.Engaging && engaged.TryGetValue(craftState.Target, out int left))
                            {
                                engaged[craftState.Target] = left - 1;
                            }

                            craftState.Target = best;
                            craftState.Engaging = best != Entity.Null;
                            craftState.Ring = -1;

                            if (craftState.Engaging)
                            {
                                engaged[best] = (engaged.TryGetValue(best, out int count) ? count : 0) + 1;

                                craftState.Approaching = true;
                                craftState.Ring = random.NextInt(0, craftSpec.AttackRings);
                                craftState.Angle = random.NextFloat(0.0F, 2.0F * math.PI);
                                craftState.Orbit = random.NextBool() ? 1.0F : -1.0F;
                                craftState.DodgeRemaining = random.NextFloat(craftSpec.DodgeInterval.x, craftSpec.DodgeInterval.y);
                            }
                        }

                        states[i] = craftState;
                    }
                }

                // 교전 중인 함재기의 자리가 대상을 둘러싼 고리를 따라 돎. 같은 대상, 같은 고리의 자리와 부딪히면 서로 반대로 돎.
                for (int i = 0; i < states.Length; i++)
                {
                    CarriedCraftComponent craftState = states[i];

                    if (absorbed[i] || craftState.Returning || !craftState.Engaging)
                    {
                        continue;
                    }

                    // 대상이 이번 틱에 격추됐으면 다음 틱에 다시 고름.
                    if (!hulls.TryGet(craftState.Target, out float2 center, out float hull, out float2 targetVelocity))
                    {
                        craftState.Target = Entity.Null;
                        craftState.Engaging = false;
                        craftState.Ring = -1;
                        states[i] = craftState;
                        continue;
                    }

                    float radius = CarrierFormation.RingRadius(hull + craftSpec.AttackRadius, craftSpec.RingGap, craftState.Ring);

                    craftState.DodgeRemaining -= deltaTime;

                    if (craftState.DodgeRemaining <= 0.0F)
                    {
                        craftState.Orbit = -craftState.Orbit;
                        craftState.DodgeRemaining = random.NextFloat(craftSpec.DodgeInterval.x, craftSpec.DodgeInterval.y);
                    }

                    for (int j = 0; j < states.Length; j++)
                    {
                        if (j == i || absorbed[j] || states[j].Returning || !states[j].Engaging
                            || states[j].Target != craftState.Target || states[j].Ring != craftState.Ring)
                        {
                            continue;
                        }

                        // 상대 자리까지의 각도. -π ~ π.
                        float delta = math.atan2(
                            math.sin(states[j].Angle - craftState.Angle),
                            math.cos(states[j].Angle - craftState.Angle)
                        );

                        if (math.abs(delta) * radius < craftSpec.SlotGap && math.sign(delta) == craftState.Orbit)
                        {
                            craftState.Orbit = -craftState.Orbit;
                        }
                    }

                    craftState.Angle += craftState.Orbit * craftSpec.OrbitSpeed / radius * deltaTime;
                    craftState.Anchor = center;
                    craftState.AnchorVelocity = targetVelocity;
                    craftState.HullRadius = hull;
                    craftState.Slot = center + CarrierFormation.Direction(craftState.Angle) * radius;
                    states[i] = craftState;
                }

                // 한 대씩 내보냄.
                carrier.LaunchRemaining = math.max(carrier.LaunchRemaining - deltaTime, 0.0F);

                if (deploying && carrier.Stored > 0 && carrier.LaunchRemaining <= 0.0F)
                {
                    carrier.Stored--;
                    carrier.LaunchRemaining = carrier.LaunchInterval;

                    var launched = new CarriedCraftComponent
                    {
                        Carrier        = entity,
                        OwnerNetworkId = owner.ValueRO.NetworkId,
                        DamageScale    = damageScale,
                        SlotIndex      = -1,
                        Ring           = -1,
                        Orbit          = random.NextBool() ? 1.0F : -1.0F,
                        DodgeRemaining = random.NextFloat(craftSpec.DodgeInterval.x, craftSpec.DodgeInterval.y),
                        Flying         = true,

                        // 모선 안에서 쏘지 않게 잠시 기다림.
                        WindUpRemaining = craftSpec.LaunchWindUp,
                    };

                    if (carrier.Order == CarrierOrder.Attack)
                    {
                        launched.Target = carrier.Target;
                        launched.Engaging = true;
                        launched.Approaching = true;
                        launched.Ring = random.NextInt(0, craftSpec.AttackRings);
                        launched.Angle = random.NextFloat(0.0F, 2.0F * math.PI);
                        launched.Anchor = carrier.Position;
                        launched.AnchorVelocity = anchorVelocity;
                        launched.HullRadius = attackHull;
                        launched.Slot = carrier.Position + CarrierFormation.Direction(launched.Angle)
                                        * CarrierFormation.RingRadius(attackHull + craftSpec.AttackRadius, craftSpec.RingGap, launched.Ring);
                    }
                    else
                    {
                        // 남은 자리 중 모선에서 가장 가까운 자리.
                        float nearest = float.MaxValue;

                        for (int j = 0; j < formationCount; j++)
                        {
                            bool taken = false;

                            for (int i = 0; i < states.Length; i++)
                            {
                                taken |= !absorbed[i] && !states[i].Returning && states[i].SlotIndex == j;
                            }

                            float2 slot = carrier.Position
                                          + CarrierFormation.RingSlot(j, formationCount, firstRadius, craftSpec.RingGap, craftSpec.SlotGap);
                            float distance = math.distancesq(position, slot);

                            if (!taken && distance < nearest)
                            {
                                nearest = distance;
                                launched.SlotIndex = j;
                                launched.Slot = slot;
                            }
                        }
                    }

                    // 크기는 프리팹 값, 위치와 회전은 모선 값.
                    LocalTransform craftTransform = SystemAPI.GetComponent<LocalTransform>(carrier.CraftPrefab);
                    craftTransform.Position = transform.ValueRO.Position;
                    craftTransform.Rotation = transform.ValueRO.Rotation;

                    SpaceshipSpecComponent craftHealth = SystemAPI.GetComponent<SpaceshipSpecComponent>(carrier.CraftPrefab);
                    craftHealth.MaxHealth *= healthScale;
                    craftHealth.Health = craftHealth.MaxHealth;

                    EnemySpaceshipMovementComponent craftMovement = SystemAPI.GetComponent<EnemySpaceshipMovementComponent>(carrier.CraftPrefab);
                    craftMovement.Velocity = velocity;

                    Entity craft = ecb.Instantiate(carrier.CraftPrefab);
                    ecb.SetComponent(craft, craftTransform);

                    // 적 우주선과 같은 소유자 -1. 주인 화면에서도 보간으로 그림.
                    ecb.SetComponent(craft, new GhostOwner { NetworkId = -1 });
                    ecb.SetComponent(craft, faction.ValueRO);
                    ecb.SetComponent(craft, craftHealth);
                    ecb.SetComponent(craft, craftMovement);
                    ecb.SetComponent(craft, new SpaceshipMovementComponent { DeltaMovement = velocity });

                    WriteOrder(ref launched, carrier, anchorVelocity, heading);
                    ecb.SetComponent(craft, launched);
                }

                for (int i = 0; i < states.Length; i++)
                {
                    if (absorbed[i])
                    {
                        continue;
                    }

                    CarriedCraftComponent craftState = states[i];

                    if (craftState.Returning)
                    {
                        craftState.Target = Entity.Null;
                        craftState.Engaging = false;
                        craftState.Ring = -1;
                        craftState.HullRadius = 0.0F;
                        craftState.Anchor = position;
                        craftState.AnchorVelocity = velocity;
                        craftState.Slot = position;
                    }
                    else if (carrier.Order != CarrierOrder.Return)
                    {
                        // 귀환 출발을 기다리는 함재기는 하던 일을 계속함.
                        WriteOrder(ref craftState, carrier, anchorVelocity, heading);
                    }

                    SystemAPI.SetComponent(mine[i], craftState);

                    // 모선과 같은 편. 파티가 바뀌면 따라 바뀜.
                    SystemAPI.SetComponent(mine[i], faction.ValueRO);
                }
            }

            ecb.Playback(state.EntityManager);
            ecb.Dispose();
            ranges.Dispose();
            mine.Dispose();
            states.Dispose();
            positions.Dispose();
            absorbed.Dispose();
            engaged.Dispose();
        }

        // 교전하지 않는 함재기의 진형 중심은 명령 지점. 교전 중이면 고리 갱신에서 대상으로 채움.
        private static void WriteOrder(ref CarriedCraftComponent craft, in CarrierComponent carrier, float2 anchorVelocity, float2 heading)
        {
            craft.Order = carrier.Order;
            craft.Heading = heading;

            if (!craft.Engaging)
            {
                craft.Target = Entity.Null;
                craft.HullRadius = 0.0F;
                craft.Anchor = carrier.Position;
                craft.AnchorVelocity = anchorVelocity;
            }
        }

        /// <summary>
        /// 교전 대상 선체 조회.
        /// </summary>
        private struct Hulls
        {
            public ComponentLookup<LocalTransform> Transforms;
            public ComponentLookup<ColliderComponent> Colliders;
            public ComponentLookup<SpaceshipSpecComponent> Specs;
            public ComponentLookup<SpaceshipMovementComponent> Movements;

            /// <summary>
            /// 선체 중심, 감싸는 원의 반지름, 이동 속도. 사라졌거나 격추됐으면 false.
            /// </summary>
            public bool TryGet(Entity target, out float2 center, out float radius, out float2 velocity)
            {
                center = float2.zero;
                radius = 0.0F;
                velocity = float2.zero;

                if (!Transforms.TryGetComponent(target, out LocalTransform transform)
                    || !Colliders.TryGetComponent(target, out ColliderComponent collider)
                    || !Specs.TryGetComponent(target, out SpaceshipSpecComponent spec)
                    || spec.Health <= 0.0F)
                {
                    return false;
                }

                ColliderWorld hull = ColliderOverlap.ToWorld(collider, transform);
                center = hull.Center;
                radius = CarrierFormation.HullRadius(hull);
                velocity = Movements.TryGetComponent(target, out SpaceshipMovementComponent movement) ? movement.DeltaMovement : float2.zero;

                return true;
            }
        }
    }
}
