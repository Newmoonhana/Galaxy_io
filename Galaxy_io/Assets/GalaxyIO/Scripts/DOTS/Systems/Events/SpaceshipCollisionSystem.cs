using GalaxyIO.DOTS.Collision;
using GalaxyIO.DOTS.Components.Actor;
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
    /// 우주선끼리 부딪힌 결과를 반영하는 시스템. 겹친 깊이만큼 밀어내고 데미지를 줌.
    /// </summary>
    [BurstCompile]
    [WorldSystemFilter(WorldSystemFilterFlags.ServerSimulation)]
    [UpdateInGroup(typeof(EventSystemGroup))]
    public partial struct SpaceshipCollisionSystem : ISystem
    {
        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<CollisionConfigComponent>();
            state.RequireForUpdate(
                SystemAPI.QueryBuilder()
                    .WithAll<SpaceshipCollisionElement, SpaceshipSpecComponent, LocalTransform>()
                    .Build()
            );
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            CollisionConfigComponent config = SystemAPI.GetSingleton<CollisionConfigComponent>();

            state.Dependency = new SpaceshipCollisionJob
            {
                Movements    = SystemAPI.GetComponentLookup<MovementComponent>(true),
                Factions     = SystemAPI.GetComponentLookup<FactionComponent>(true),
                Owners       = SystemAPI.GetComponentLookup<GhostOwner>(true),
                EnemyNames   = SystemAPI.GetComponentLookup<EnemyNameComponent>(true),
                Records      = SystemAPI.GetBufferLookup<DamageRecordElement>(),
                Reports      = SystemAPI.GetBufferLookup<DamageReportElement>(),
                KillingBlows = SystemAPI.GetComponentLookup<KillingBlowComponent>(),
                RamDamage    = config.RamDamage,
                RamMinSpeed  = config.RamMinSpeed,
                GroupLimit   = config.GroupRepulsion,
                ElapsedTime  = SystemAPI.Time.ElapsedTime,
            }.ScheduleParallel(state.Dependency);

            // 데미지 판정이 상대의 속도를 읽으므로 그 뒤에 속도를 바꿈.
            state.Dependency = new CollisionResponseJob
            {
                Restitution = config.Restitution,
                GroupLimit  = config.GroupRepulsion,
            }.ScheduleParallel(state.Dependency);
        }
    }

    /// <summary>
    /// 두 우주선의 상대 속력과 두께로 데미지를 줌. 식이 대칭이라 각 우주선이 자기 몫만 계산함.
    /// 같은 파티끼리는 밀어내기만 하고 데미지는 없음.
    /// </summary>
    [BurstCompile]
    public partial struct SpaceshipCollisionJob : IJobEntity
    {
        [ReadOnly]
        public ComponentLookup<MovementComponent> Movements;

        [ReadOnly]
        public ComponentLookup<FactionComponent> Factions;

        [ReadOnly]
        public ComponentLookup<GhostOwner> Owners;

        [ReadOnly]
        public ComponentLookup<EnemyNameComponent> EnemyNames;

        // 아래 셋은 자기 Entity 에만 씀. Entity 마다 한 번씩만 실행돼 병렬이어도 겹치지 않음.
        [NativeDisableParallelForRestriction]
        public BufferLookup<DamageRecordElement> Records;

        [NativeDisableParallelForRestriction]
        public BufferLookup<DamageReportElement> Reports;

        [NativeDisableParallelForRestriction]
        public ComponentLookup<KillingBlowComponent> KillingBlows;

        // 상대 속력 1당 데미지
        public float RamDamage;

        // 상대 속력이 이보다 느린 충돌은 데미지 없음
        public float RamMinSpeed;

        // 같은 모선에 딸린 사이가 한 틱에 밀어내는 최대 거리
        public float GroupLimit;

        public double ElapsedTime;

        private void Execute(
            Entity entity,
            in DynamicBuffer<SpaceshipCollisionElement> messages,
            ref LocalTransform transform,
            ref SpaceshipSpecComponent spec,
            in MovementComponent movement,
            in FactionComponent faction)
        {
            float2 push = float2.zero;
            float damage = 0.0F;

            // 이번 틱에 가장 세게 부딪힌 상대. 이 충돌로 격추되면 막타로 남김.
            Entity strongest = Entity.Null;
            float strongestHit = 0.0F;

            for (int i = 0; i < messages.Length; i++)
            {
                SpaceshipCollisionElement element = messages[i];
                CollisionMessage message = element.Message;
                float hit = 0.0F;

                // 이번 틱에 파괴된 상대는 이동 정보가 없음. 같은 파티와 함께 밀어내기만 남김.
                if (Movements.TryGetComponent(message.Other, out MovementComponent other)
                    && !Faction.IsSameParty(faction.Party, Factions[message.Other].Party))
                {
                    float2 relative = movement.Velocity - other.Velocity;

                    // 서로 다가가는 중일 때만 데미지. 튕겨 나간 뒤 맞닿은 틱, 살짝 스친 충돌은 제외.
                    if (math.dot(relative, message.Normal) < 0.0F && math.lengthsq(relative) >= RamMinSpeed * RamMinSpeed)
                    {
                        // 마주 올수록 커지고, 같은 방향으로 나란히 가면 0에 가까워짐.
                        // 상대가 두꺼울수록 더 아픔. 두께가 같으면 1배, 최대 2배.
                        hit = math.length(relative) * RamDamage * element.DamageShare * 2.0F;
                    }
                }

                damage += hit;

                if (hit > strongestHit)
                {
                    strongest = message.Other;
                    strongestHit = hit;
                }

                // 플레이어와 부딪힌 데미지만 어시스트 판정용으로 기록함.
                if (hit > 0.0F
                    && Owners.TryGetComponent(message.Other, out GhostOwner otherOwner)
                    && otherOwner.NetworkId > 0
                    && Records.TryGetBuffer(entity, out DynamicBuffer<DamageRecordElement> records))
                {
                    DamageRecord.Add(records, otherOwner.NetworkId, hit, ElapsedTime);
                }

                // 데미지 숫자는 두 선체의 중간에 띄움.
                if (hit > 0.0F && Reports.TryGetBuffer(entity, out DynamicBuffer<DamageReportElement> reports))
                {
                    reports.Add(new DamageReportElement
                    {
                        AttackerNetworkId = Owners.TryGetComponent(message.Other, out GhostOwner attacker) ? attacker.NetworkId : -1,
                        Amount            = hit,
                        Position          = (transform.Position.xy + message.OtherCenter) * 0.5F,
                        Turret            = ItemId.None,
                    });
                }

                // 두께로 나눈 몫만큼 서로 물러나면 딱 맞닿음. 두께가 같으면 절반씩.
                // 빠를수록 한 틱에 깊이 박히므로 밀려나는 양도 자동으로 커짐. 같은 모선에 딸린 사이는 조금만 밀어냄.
                float pushed = message.Depth * element.PushShare;
                push += message.Normal * (element.Grouped ? math.min(pushed, GroupLimit) : pushed);
            }

            // 판정에 쓴 위치는 밀어내기 전 위치라, 계산 순서에 관계없이 결과가 같음.
            transform.Position = new float3(transform.Position.xy + push, transform.Position.z);

            // 스치듯 지나가 데미지가 0인 충돌은 수리 대기를 걸지 않음.
            if (damage <= 0.0F)
            {
                return;
            }

            float healthBefore = spec.Health;
            spec.Health = math.max(spec.Health - damage, 0.0F);
            spec.RepairCooldown = spec.RepairDelay;

            // 체력을 0으로 만든 충돌만 막타로 남김.
            if (healthBefore > 0.0F && spec.Health <= 0.0F && KillingBlows.HasComponent(entity))
            {
                KillingBlows[entity] = new KillingBlowComponent
                {
                    Type      = KillType.Ram,
                    NetworkId = Owners.TryGetComponent(strongest, out GhostOwner killer) ? killer.NetworkId : -1,
                    EnemyName = EnemyNames.TryGetComponent(strongest, out EnemyNameComponent enemyName)
                        ? enemyName.Value : default,
                };
            }
        }
    }

    /// <summary>
    /// 부딪힌 우주선의 속도에 튕겨 나가는 속도를 더함. 다음 틱 조종이 이 속도에서 시작함.
    /// </summary>
    [BurstCompile]
    [WithAll(typeof(Simulate))]
    [WithAny(typeof(PlayerSpaceshipMovementComponent), typeof(EnemySpaceshipMovementComponent))]
    public partial struct CollisionResponseJob : IJobEntity
    {
        public float Restitution;
        public float GroupLimit;

        private void Execute(in DynamicBuffer<SpaceshipCollisionElement> messages, ref MovementComponent movement)
        {
            movement.Velocity += CollisionResponse.Impulse(movement.Velocity, messages, Restitution, GroupLimit);
        }
    }
}
