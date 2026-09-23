using GalaxyIO.DOTS.Collision;
using GalaxyIO.DOTS.Components.Actor;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.NetCode;
using Unity.Transforms;

namespace GalaxyIO.DOTS.Systems
{
    /// <summary>
    /// 우주선에 적용할 데미지. 병렬 Job이 다른 Entity에 직접 쓸 수 없어 큐를 거침.
    /// </summary>
    public struct SpaceshipDamage
    {
        public Entity Spaceship;
        public float Amount;

        // 투사체를 쏜 우주선. 이미 파괴됐으면 Entity.Null.
        public Entity Instigator;

        // 판정 시점의 Instigator 위치
        public float2 InstigatorPosition;

        // 투사체 소유자. 쏜 우주선이 파괴돼도 남음. AI 는 -1.
        public int InstigatorNetworkId;

        // 쏜 AI 의 표시 이름. 플레이어거나 쏜 AI 가 파괴됐으면 비어 있음.
        public FixedString32Bytes InstigatorEnemyName;
    }

    /// <summary>
    /// 명중한 투사체의 데미지를 우주선에 반영하는 시스템.
    /// 투사체 제거는 <see cref="SharedProjectileSystem"/>이 담당하므로 Lifetime만 만료시킴.
    /// </summary>
    [BurstCompile]
    [WorldSystemFilter(WorldSystemFilterFlags.ServerSimulation)]
    [UpdateInGroup(typeof(CollisionDetectionSystemGroup))]
    [UpdateAfter(typeof(CollisionDetectionSystem))]
    public partial struct ProjectileDamageSystem : ISystem
    {
        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate(
                SystemAPI.QueryBuilder().WithAll<ProjectileHitElement, SharedProjectileComponent>().Build()
            );
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var damages = new NativeQueue<SpaceshipDamage>(Allocator.TempJob);

            state.Dependency = new ProjectileDamageJob
            {
                Transforms = SystemAPI.GetComponentLookup<LocalTransform>(true),
                Colliders  = SystemAPI.GetComponentLookup<ColliderComponent>(true),
                EnemyNames = SystemAPI.GetComponentLookup<EnemyNameComponent>(true),
                Damages    = damages.AsParallelWriter(),
            }.ScheduleParallel(state.Dependency);

            state.Dependency = new ApplySpaceshipDamageJob
            {
                Damages      = damages,
                Specs        = SystemAPI.GetComponentLookup<SpaceshipSpecComponent>(),
                Events       = SystemAPI.GetBufferLookup<EnemyEventElement>(),
                Records      = SystemAPI.GetBufferLookup<DamageRecordElement>(),
                KillingBlows = SystemAPI.GetComponentLookup<KillingBlowComponent>(),
                ElapsedTime  = SystemAPI.Time.ElapsedTime,
            }.Schedule(state.Dependency);

            state.Dependency = damages.Dispose(state.Dependency);
        }
    }

    /// <summary>
    /// 명중 메시지를 데미지로 바꾸고 투사체를 만료시킴.
    /// </summary>
    [BurstCompile]
    [WithAll(typeof(Simulate))]
    public partial struct ProjectileDamageJob : IJobEntity
    {
        [ReadOnly]
        public ComponentLookup<LocalTransform> Transforms;

        [ReadOnly]
        public ComponentLookup<ColliderComponent> Colliders;

        [ReadOnly]
        public ComponentLookup<EnemyNameComponent> EnemyNames;

        public NativeQueue<SpaceshipDamage>.ParallelWriter Damages;

        private void Execute(
            in DynamicBuffer<ProjectileHitElement> messages,
            ref SharedProjectileComponent projectile,
            in GhostOwner owner)
        {
            if (messages.IsEmpty)
            {
                return;
            }

            FindInstigator(projectile.Owner, out Entity instigator, out float2 instigatorPosition);

            EnemyNames.TryGetComponent(projectile.Owner, out EnemyNameComponent enemyName);

            Damages.Enqueue(new SpaceshipDamage
            {
                Spaceship           = messages[0].Message.Other,
                Amount              = projectile.Damage,
                Instigator          = instigator,
                InstigatorPosition  = instigatorPosition,
                InstigatorNetworkId = owner.NetworkId,
                InstigatorEnemyName = enemyName.Value,
            });

            projectile.Lifetime = 0.0F;
        }

        /// <summary>
        /// 투사체를 쏜 우주선의 현재 위치를 찾음. 이미 파괴됐으면 Entity.Null.
        /// </summary>
        private void FindInstigator(Entity owner, out Entity instigator, out float2 position)
        {
            if (Colliders.TryGetComponent(owner, out ColliderComponent collider)
                && Transforms.TryGetComponent(owner, out LocalTransform transform))
            {
                instigator = owner;
                position = ColliderOverlap.ToWorld(collider, transform).Center;

                return;
            }

            instigator = Entity.Null;
            position = float2.zero;
        }
    }

    /// <summary>
    /// 쌓인 데미지를 실제 체력에 반영하고 AI 에게 피격 이벤트를 남김. 다른 Entity에 쓰므로 단일 스레드로 돎.
    /// </summary>
    [BurstCompile]
    public struct ApplySpaceshipDamageJob : IJob
    {
        public NativeQueue<SpaceshipDamage> Damages;
        public ComponentLookup<SpaceshipSpecComponent> Specs;
        public BufferLookup<EnemyEventElement> Events;
        public BufferLookup<DamageRecordElement> Records;
        public ComponentLookup<KillingBlowComponent> KillingBlows;
        public double ElapsedTime;

        public void Execute()
        {
            while (Damages.TryDequeue(out SpaceshipDamage damage))
            {
                // 스치듯 지나가 데미지가 0인 충돌은 수리 대기를 걸지 않음.
                if (damage.Amount <= 0.0F || !Specs.HasComponent(damage.Spaceship))
                {
                    continue;
                }

                SpaceshipSpecComponent spec = Specs[damage.Spaceship];
                float healthBefore = spec.Health;
                spec.Health = math.max(spec.Health - damage.Amount, 0.0F);
                spec.RepairCooldown = spec.RepairDelay;
                Specs[damage.Spaceship] = spec;

                // 플레이어가 입힌 데미지만 어시스트 판정용으로 기록함.
                if (damage.InstigatorNetworkId > 0
                    && Records.TryGetBuffer(damage.Spaceship, out DynamicBuffer<DamageRecordElement> records))
                {
                    DamageRecord.Add(records, damage.InstigatorNetworkId, damage.Amount, ElapsedTime);
                }

                // 체력을 0으로 만든 타격만 막타로 남김.
                if (healthBefore > 0.0F && spec.Health <= 0.0F && KillingBlows.HasComponent(damage.Spaceship))
                {
                    KillingBlows[damage.Spaceship] = new KillingBlowComponent
                    {
                        Type      = KillType.Indirect,
                        NetworkId = damage.InstigatorNetworkId,
                        EnemyName = damage.InstigatorEnemyName,
                    };
                }

                // 이벤트 버퍼를 가진 AI 에게만 알림.
                if (damage.Instigator != Entity.Null
                    && Events.TryGetBuffer(damage.Spaceship, out DynamicBuffer<EnemyEventElement> events))
                {
                    events.Add(new EnemyEventElement
                    {
                        ID                 = EnemyEventID.ProjectileHit,
                        Instigator         = damage.Instigator,
                        InstigatorPosition = damage.InstigatorPosition,
                    });
                }
            }
        }
    }
}
