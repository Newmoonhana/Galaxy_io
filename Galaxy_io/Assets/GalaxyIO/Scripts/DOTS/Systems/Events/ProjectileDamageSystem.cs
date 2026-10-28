using GalaxyIO.DOTS.Collision;
using GalaxyIO.DOTS.Components.Actor;
using GalaxyIO.DOTS.Components.Singleton;
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

        // 쏜 우주선의 주인. 투사체 소유자와 다르면(자동 포탑, 함재기) 쏜 우주선이 남아 있을 때만 앎. AI 는 -1.
        public int InstigatorNetworkId;

        // 쏜 AI 의 표시 이름. 플레이어거나 쏜 AI 가 파괴됐으면 비어 있음.
        public FixedString32Bytes InstigatorEnemyName;

        // 맞은 지점. 데미지 숫자를 띄울 자리.
        public float2 Position;

        // 쏜 포탑. 명중음을 고르는 데 씀.
        public ItemId Turret;
    }

    /// <summary>
    /// AoE 폭발 데미지. 반경 안의 우주선에 거리 비례 데미지를 줌.
    /// </summary>
    public struct ProjectileExplosion
    {
        public float2 Center;
        public float Radius;

        // 투사체 진영과 파티. 동맹과 같은 파티는 맞지 않음.
        public FactionComponent Faction;

        // 직격 피해를 입은 우주선. 폭발 데미지에서 제외.
        public Entity DirectHit;

        // 폭발 중심에서 줄 데미지와 가해자 정보. Spaceship 은 비어 있음.
        public SpaceshipDamage Damage;
    }

    /// <summary>
    /// 명중한 투사체의 직격, 관통, 폭발 데미지를 우주선에 반영하는 시스템.
    /// 투사체 제거는 <see cref="SharedProjectileSystem"/>이 담당하므로 Lifetime만 만료시킴.
    /// </summary>
    [BurstCompile]
    [WorldSystemFilter(WorldSystemFilterFlags.ServerSimulation)]
    [UpdateInGroup(typeof(EventSystemGroup))]
    public partial struct ProjectileDamageSystem : ISystem
    {
        private EntityQuery projectileQuery;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            projectileQuery = SystemAPI.QueryBuilder()
                .WithAll<ProjectileHitElement, SharedProjectileComponent>()
                .Build();

            state.RequireForUpdate(projectileQuery);
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var damages = new NativeQueue<SpaceshipDamage>(Allocator.TempJob);

            // 투사체 하나가 폭발 하나까지만 냄.
            var explosions = new NativeList<ProjectileExplosion>(
                projectileQuery.CalculateEntityCount(),
                Allocator.TempJob
            );

            state.Dependency = new ProjectileDamageJob
            {
                Transforms = SystemAPI.GetComponentLookup<LocalTransform>(true),
                Colliders  = SystemAPI.GetComponentLookup<ColliderComponent>(true),
                EnemyNames = SystemAPI.GetComponentLookup<EnemyNameComponent>(true),
                Specs      = SystemAPI.GetComponentLookup<SpaceshipSpecComponent>(true),
                Owners     = SystemAPI.GetComponentLookup<GhostOwner>(true),
                Crafts     = SystemAPI.GetComponentLookup<CarriedCraftComponent>(true),
                Damages    = damages.AsParallelWriter(),
                Explosions = explosions.AsParallelWriter(),
            }.ScheduleParallel(state.Dependency);

            state.Dependency = new ExplosionDamageJob
            {
                Explosions = explosions,
                Damages    = damages.AsParallelWriter(),
            }.ScheduleParallel(state.Dependency);

            state.Dependency = new ApplySpaceshipDamageJob
            {
                Damages      = damages,
                Specs        = SystemAPI.GetComponentLookup<SpaceshipSpecComponent>(),
                Events       = SystemAPI.GetBufferLookup<EnemyEventElement>(),
                Records      = SystemAPI.GetBufferLookup<DamageRecordElement>(),
                Reports      = SystemAPI.GetBufferLookup<DamageReportElement>(),
                KillingBlows = SystemAPI.GetComponentLookup<KillingBlowComponent>(),
                ElapsedTime  = SystemAPI.Time.ElapsedTime,
            }.Schedule(state.Dependency);

            state.Dependency = damages.Dispose(state.Dependency);
            state.Dependency = explosions.Dispose(state.Dependency);
        }
    }

    /// <summary>
    /// 명중 메시지를 먼저 닿은 순으로 데미지로 바꿈. 관통력이 다하면 투사체를 Dead 로 바꾸고 폭발을 남김.
    /// </summary>
    [BurstCompile]
    [WithAll(typeof(Simulate))]
    public partial struct ProjectileDamageJob : IJobEntity
    {
        // 명중으로 끝난 투사체를 남겨 두는 시간(초). 마지막 명중이 스냅샷에 실릴 여유.
        private const float DeadLinger = 0.25F;

        [ReadOnly]
        public ComponentLookup<LocalTransform> Transforms;

        [ReadOnly]
        public ComponentLookup<ColliderComponent> Colliders;

        [ReadOnly]
        public ComponentLookup<EnemyNameComponent> EnemyNames;

        [ReadOnly]
        public ComponentLookup<SpaceshipSpecComponent> Specs;

        [ReadOnly]
        public ComponentLookup<GhostOwner> Owners;

        [ReadOnly]
        public ComponentLookup<CarriedCraftComponent> Crafts;

        public NativeQueue<SpaceshipDamage>.ParallelWriter Damages;

        public NativeList<ProjectileExplosion>.ParallelWriter Explosions;

        private void Execute(
            in DynamicBuffer<ProjectileHitElement> messages,
            DynamicBuffer<ProjectilePiercedElement> pierced,
            ref SharedProjectileComponent projectile,
            in LocalTransform transform,
            in FactionComponent faction,
            in GhostOwner owner)
        {
            if (messages.IsEmpty)
            {
                return;
            }

            FindInstigator(projectile.Owner, out Entity instigator, out float2 instigatorPosition);

            EnemyNames.TryGetComponent(projectile.Owner, out EnemyNameComponent enemyName);

            // 함재기가 쏜 것은 모선 주인의 몫.
            int instigatorNetworkId = owner.NetworkId;

            if (Crafts.TryGetComponent(projectile.Owner, out CarriedCraftComponent craft))
            {
                instigatorNetworkId = craft.OwnerNetworkId;
            }
            else if (Owners.TryGetComponent(projectile.Owner, out GhostOwner shooter))
            {
                instigatorNetworkId = shooter.NetworkId;
            }

            var damage = new SpaceshipDamage
            {
                Amount              = projectile.Damage,
                Instigator          = instigator,
                InstigatorPosition  = instigatorPosition,
                InstigatorNetworkId = instigatorNetworkId,
                InstigatorEnemyName = enemyName.Value,
                Turret              = projectile.Turret,
            };

            float2 start = transform.Position.xy - projectile.Displacement;

            for (int i = 0; i < messages.Length; i++)
            {
                CollisionMessage message = messages[i].Message;

                // 이동 경로 위에서 맞은 우주선에 가장 가까운 점. 명중 연출과 폭발 중심.
                projectile.HitCount++;
                projectile.HitPosition = ColliderOverlap.ClosestOnPath(start, projectile.Displacement, message.OtherCenter);

                damage.Spaceship = message.Other;
                damage.Position = projectile.HitPosition;
                Damages.Enqueue(damage);

                Specs.TryGetComponent(message.Other, out SpaceshipSpecComponent spec);
                projectile.Pierce -= spec.Thickness;

                if (projectile.Pierce > 0.0F)
                {
                    pierced.Add(new ProjectilePiercedElement { Spaceship = message.Other });
                    continue;
                }

                projectile.Dead = true;
                projectile.Lifetime = DeadLinger;

                if (projectile.ExplosionRadius > 0.0F)
                {
                    Explosions.AddNoResize(new ProjectileExplosion
                    {
                        Center    = projectile.HitPosition,
                        Radius    = projectile.ExplosionRadius,
                        Faction   = faction,
                        DirectHit = message.Other,
                        Damage    = damage,
                    });
                }

                return;
            }
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
    /// 폭발 반경에 걸린 우주선마다 데미지를 쌓음. 선체가 폭발 중심에 가까울수록 세게 맞음.
    /// 우주선은 되감지 않고 현재 위치로 판정함.
    /// </summary>
    [BurstCompile]
    [WithAll(typeof(SpaceshipSpecComponent))]
    public partial struct ExplosionDamageJob : IJobEntity
    {
        [ReadOnly]
        public NativeList<ProjectileExplosion> Explosions;

        public NativeQueue<SpaceshipDamage>.ParallelWriter Damages;

        private void Execute(
            Entity entity,
            in LocalTransform transform,
            in ColliderComponent collider,
            in FactionComponent faction)
        {
            if (Explosions.IsEmpty)
            {
                return;
            }

            ColliderWorld hull = ColliderOverlap.ToWorld(collider, transform);

            for (int i = 0; i < Explosions.Length; i++)
            {
                ProjectileExplosion explosion = Explosions[i];

                // 직격당한 우주선, 쏜 우주선, 동맹, 같은 파티는 제외.
                if (entity == explosion.DirectHit
                    || entity == explosion.Damage.Instigator
                    || Faction.IsFriendly(explosion.Faction, faction))
                {
                    continue;
                }

                var area = new ColliderWorld
                {
                    Shape   = ColliderShape.Circle,
                    Center  = explosion.Center,
                    Extents = new float2(explosion.Radius, 0.0F),
                };

                // 원 판정의 깊이 = 반경 - 선체까지 거리. 중심이 선체 안이면 반경 이상.
                if (!ColliderOverlap.Test(area, hull, out ColliderHit hit))
                {
                    continue;
                }

                SpaceshipDamage damage = explosion.Damage;
                damage.Spaceship = entity;
                damage.Position = hull.Center;
                damage.Amount *= math.saturate(hit.Depth / explosion.Radius);
                Damages.Enqueue(damage);
            }
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
        public BufferLookup<DamageReportElement> Reports;
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

                if (Reports.TryGetBuffer(damage.Spaceship, out DynamicBuffer<DamageReportElement> reports))
                {
                    reports.Add(new DamageReportElement
                    {
                        AttackerNetworkId = damage.InstigatorNetworkId,
                        Amount            = damage.Amount,
                        Position          = damage.Position,
                        Turret            = damage.Turret,
                    });
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