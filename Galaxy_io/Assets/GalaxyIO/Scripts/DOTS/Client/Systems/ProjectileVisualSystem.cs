using GalaxyIO.DOTS.Components.Actor;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;

namespace GalaxyIO.DOTS.Client.Systems
{
    /// <summary>
    /// 투사체 모습을 맡는 시스템. 감춰진(Hidden) 투사체의 스프라이트를 끄고,
    /// 파티클/트레일 연출을 붙여 따라다니게 함. 투사체가 사라지거나 감춰지면 연출도 지움.
    /// 연출 재생 속도를 투사체 수명에 맞춰 파티클 한 번의 수명이 투사체 수명 전체에 걸치게 함.
    /// 트레일이 있는 연출은 투사체가 사라지면 파티클만 지우고 그 자리에 남아 꼬리가 줄어든 뒤 지워짐.
    /// 월드 공간 파티클(연기 등)은 트레일처럼 다룸. 재생 속도를 바꾸지 않고, 투사체가 사라지면 방출만 멈춤.
    /// </summary>
    [WorldSystemFilter(WorldSystemFilterFlags.ClientSimulation)]
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateBefore(typeof(TransformSystemGroup))]
    public partial struct ProjectileVisualSystem : ISystem
    {
        public void OnCreate(ref SystemState state)
        {
            state.RequireAnyForUpdate(
                SystemAPI.QueryBuilder().WithAll<SharedProjectileComponent>().Build(),
                SystemAPI.QueryBuilder().WithAll<ProjectileVisualOwnerComponent>().Build()
            );
        }

        public void OnUpdate(ref SystemState state)
        {
            EntityManager entityManager = state.EntityManager;

            // 감춰진 투사체의 스프라이트를 끔. Entity 자체는 스냅샷을 계속 받도록 켜 둠.
            foreach (
                var (shared, sprite) in
                SystemAPI.Query<RefRO<SharedProjectileComponent>, SystemAPI.ManagedAPI.UnityEngineComponent<SpriteRenderer>>()
            ) {
                if (shared.ValueRO.Hidden && sprite.Value.enabled)
                {
                    sprite.Value.enabled = false;
                }
            }

            // 연출이 없는 새 투사체. 스폰은 구조 변경이라 순회 뒤에 처리함.
            var pending = new NativeList<Entity>(Allocator.Temp);

            foreach (
                var (visual, shared, projectile) in
                SystemAPI.Query<RefRO<ProjectileVisualComponent>, RefRO<SharedProjectileComponent>>()
                    .WithEntityAccess()
            ) {
                if (visual.ValueRO.Instance == Entity.Null && !shared.ValueRO.Hidden && !shared.ValueRO.Dead)
                {
                    pending.Add(projectile);
                }
            }

            foreach (Entity projectile in pending)
            {
                ProjectileVisualComponent visual = entityManager.GetComponentData<ProjectileVisualComponent>(projectile);
                LocalTransform projectileTransform = entityManager.GetComponentData<LocalTransform>(projectile);
                float lifetime = entityManager.GetComponentData<SharedProjectileComponent>(projectile).Lifetime;

                // 프리팹의 크기는 그대로 쓰고 위치/회전만 투사체를 따름.
                LocalTransform visualTransform = entityManager.GetComponentData<LocalTransform>(visual.Prefab);
                visualTransform.Position = projectileTransform.Position;
                visualTransform.Rotation = projectileTransform.Rotation;

                visual.Instance = entityManager.Instantiate(visual.Prefab);
                entityManager.SetComponentData(visual.Instance, visualTransform);
                entityManager.AddComponentData(visual.Instance, new ProjectileVisualOwnerComponent { Projectile = projectile });
                entityManager.SetComponentData(projectile, visual);

                bool hasTrail = false;
                NativeArray<EffectPartElement> parts = entityManager.GetBuffer<EffectPartElement>(visual.Instance).ToNativeArray(Allocator.Temp);

                foreach (EffectPartElement part in parts)
                {
                    TrailRenderer trail = entityManager.HasComponent<TrailRenderer>(part.Value)
                        ? entityManager.GetComponentObject<TrailRenderer>(part.Value)
                        : null;
                    ParticleSystem particle = trail == null ? entityManager.GetComponentObject<ParticleSystem>(part.Value) : null;

                    // 월드 공간 파티클은 지나간 자리에 남는 자취(e.g. 미사일 연기). 트레일처럼 다룸.
                    if (trail != null || particle.main.simulationSpace == ParticleSystemSimulationSpace.World)
                    {
                        // 꼬리가 프리팹 위치에서 시작하지 않도록 바로 옮기고 비움. 자식은 한 단계까지만 위치를 맞춤.
                        float3 position = part.Value == visual.Instance
                            ? visualTransform.Position
                            : visualTransform.TransformPoint(entityManager.GetComponentData<LocalTransform>(part.Value).Position);

                        if (trail != null)
                        {
                            trail.transform.SetPositionAndRotation(position, visualTransform.Rotation);
                            trail.Clear();
                        }
                        else
                        {
                            particle.transform.SetPositionAndRotation(position, visualTransform.Rotation);
                            particle.Clear();
                        }

                        hasTrail = true;
                    }
                    else if (lifetime > 0.0F)
                    {
                        // 파티클 수명 / 재생 속도 = 투사체 수명.
                        ParticleSystem.MainModule main = particle.main;
                        main.simulationSpeed = main.startLifetime.constantMax / lifetime;
                    }
                }

                parts.Dispose();

                // 트레일이 있으면 따라다니는 동안은 제거하지 않음. 투사체가 사라지면 꼬리가 줄어들 시간만큼 남김.
                if (hasTrail || lifetime > 0.0F)
                {
                    entityManager.SetComponentData(visual.Instance, new EffectLifetimeComponent
                    {
                        Remaining = hasTrail ? float.MaxValue : lifetime,
                    });
                }
            }

            pending.Dispose();

            // Lookup 을 메인 스레드에서 직접 읽고 씀. 이동 Job 등 LocalTransform 을 쓰는 예약된 Job 을 먼저 끝냄.
            state.CompleteDependency();

            ComponentLookup<LocalTransform> transforms = SystemAPI.GetComponentLookup<LocalTransform>();
            ComponentLookup<SharedProjectileComponent> shareds = SystemAPI.GetComponentLookup<SharedProjectileComponent>(true);
            BufferLookup<EffectPartElement> partBuffers = SystemAPI.GetBufferLookup<EffectPartElement>(true);

            var ecb = new EntityCommandBuffer(Allocator.Temp);

            foreach (
                var (owner, entity) in
                SystemAPI.Query<RefRO<ProjectileVisualOwnerComponent>>().WithEntityAccess()
            ) {
                Entity projectile = owner.ValueRO.Projectile;

                if (!shareds.TryGetComponent(projectile, out SharedProjectileComponent shared)
                    || shared.Hidden
                    || shared.Dead)
                {
                    // 파티클은 투사체와 함께 바로 지우고, 자취(트레일, 월드 공간 파티클)는 사라질 때까지 남김.
                    float trailTime = 0.0F;
                    bool hasTrail = false;

                    foreach (EffectPartElement part in partBuffers[entity])
                    {
                        if (entityManager.HasComponent<TrailRenderer>(part.Value))
                        {
                            TrailRenderer trail = entityManager.GetComponentObject<TrailRenderer>(part.Value);
                            trail.emitting = false;
                            trailTime = math.max(trailTime, trail.time);
                            hasTrail = true;
                            continue;
                        }

                        ParticleSystem particle = entityManager.GetComponentObject<ParticleSystem>(part.Value);

                        if (particle.main.simulationSpace == ParticleSystemSimulationSpace.World)
                        {
                            particle.Stop(true, ParticleSystemStopBehavior.StopEmitting);
                            trailTime = math.max(trailTime, particle.main.startLifetime.constantMax);
                            hasTrail = true;
                        }
                        else
                        {
                            particle.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
                        }
                    }

                    if (hasTrail)
                    {
                        ecb.RemoveComponent<ProjectileVisualOwnerComponent>(entity);
                        ecb.SetComponent(entity, new EffectLifetimeComponent { Remaining = trailTime });
                    }
                    else
                    {
                        ecb.DestroyEntity(entity);
                    }
                    continue;
                }

                LocalTransform visualTransform = transforms[entity];
                visualTransform.Position = transforms[projectile].Position;
                visualTransform.Rotation = transforms[projectile].Rotation;
                transforms[entity] = visualTransform;
            }

            ecb.Playback(entityManager);
            ecb.Dispose();
        }
    }
}
