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
                Damages    = damages.AsParallelWriter(),
            }.ScheduleParallel(state.Dependency);

            state.Dependency = new ApplySpaceshipDamageJob
            {
                Damages = damages,
                Specs   = SystemAPI.GetComponentLookup<SpaceshipSpecComponent>(),
                Events  = SystemAPI.GetBufferLookup<EnemyEventElement>(),
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

        public NativeQueue<SpaceshipDamage>.ParallelWriter Damages;

        private void Execute(
            in DynamicBuffer<ProjectileHitElement> messages,
            ref SharedProjectileComponent projectile)
        {
            if (messages.IsEmpty)
            {
                return;
            }

            FindInstigator(projectile.Owner, out Entity instigator, out float2 instigatorPosition);

            Damages.Enqueue(new SpaceshipDamage
            {
                Spaceship          = messages[0].Message.Other,
                Amount             = projectile.Damage,
                Instigator         = instigator,
                InstigatorPosition = instigatorPosition,
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
                spec.Health = math.max(spec.Health - damage.Amount, 0.0F);
                spec.RepairCooldown = spec.RepairDelay;
                Specs[damage.Spaceship] = spec;

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
