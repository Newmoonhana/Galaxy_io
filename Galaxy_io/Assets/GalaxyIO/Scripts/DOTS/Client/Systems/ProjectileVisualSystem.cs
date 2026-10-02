using GalaxyIO.DOTS.Components.Actor;
using Unity.Collections;
using Unity.Entities;
using Unity.Transforms;
using UnityEngine;

namespace GalaxyIO.DOTS.Client.Systems
{
    /// <summary>
    /// 투사체 모습을 맡는 시스템. 감춰진(Hidden) 투사체의 스프라이트를 끄고,
    /// 파티클 연출을 붙여 따라다니게 함. 투사체가 사라지거나 감춰지면 연출도 지움.
    /// 연출 재생 속도를 투사체 수명에 맞춰 파티클 한 번의 수명이 투사체 수명 전체에 걸치게 함.
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

                if (lifetime <= 0.0F || !entityManager.HasComponent<ParticleSystem>(visual.Instance))
                {
                    continue;
                }

                // 파티클 수명 / 재생 속도 = 투사체 수명.
                ParticleSystem.MainModule main = entityManager.GetComponentObject<ParticleSystem>(visual.Instance).main;
                main.simulationSpeed = main.startLifetime.constantMax / lifetime;

                if (entityManager.HasComponent<EffectLifetimeComponent>(visual.Instance))
                {
                    entityManager.SetComponentData(visual.Instance, new EffectLifetimeComponent { Remaining = lifetime });
                }
            }

            pending.Dispose();

            // Lookup 을 메인 스레드에서 직접 읽고 씀. 이동 Job 등 LocalTransform 을 쓰는 예약된 Job 을 먼저 끝냄.
            state.CompleteDependency();

            ComponentLookup<LocalTransform> transforms = SystemAPI.GetComponentLookup<LocalTransform>();
            ComponentLookup<SharedProjectileComponent> shareds = SystemAPI.GetComponentLookup<SharedProjectileComponent>(true);

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
                    ecb.DestroyEntity(entity);
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
