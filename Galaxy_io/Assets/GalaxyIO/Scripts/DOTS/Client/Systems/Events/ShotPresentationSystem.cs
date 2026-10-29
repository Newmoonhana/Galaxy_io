using System;
using GalaxyIO.DOTS.Client.Components.Message;
using GalaxyIO.DOTS.Client.Components.Singleton;
using GalaxyIO.DOTS.Components.Actor;
using GalaxyIO.DOTS.Components.Singleton;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.NetCode;
using Unity.Transforms;
using UnityEngine;

namespace GalaxyIO.DOTS.Client.Systems
{
    /// <summary>
    /// 새로 나타난 투사체 자리에 발사 연출을 띄우고 발사음을 요청하는 시스템.
    /// 한 우주선의 같은 발사음은 프레임당 한 번만 재생함.
    /// 발사 방향으로 움직이던 만큼 우주선 속도를 연출 파티클에 물려줌(Inherit Velocity 모듈).
    /// </summary>
    [WorldSystemFilter(WorldSystemFilterFlags.ClientSimulation)]
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateAfter(typeof(AutoTurretProjectileShiftSystem))]
    [UpdateAfter(typeof(LocalProjectileSystem))]
    [UpdateAfter(typeof(ProjectileShotReceiveSystem))]
    [UpdateBefore(typeof(TransformSystemGroup))]
    public partial struct ShotPresentationSystem : ISystem
    {
        // 이보다 오래된 투사체는 발사가 아니라 복제 범위에 새로 들어온 것으로 봄.
        private const int MaxShotAgeTicks = 6;

        // 마지막 리스너 위치. 파괴되는 순간의 자기 폭발음을 살리는 데 씀.
        // 리스폰 대기 중(Dead)에는 이 캐시를 쓰지 않음 - 사망 지점 주변 소리까지 들리면 안 됨.
        private float2 lastListener;
        private bool hasLastListener;

        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<NetworkTime>();
            state.RequireForUpdate<ItemRegistryElement>();
            state.RequireForUpdate(SystemAPI.QueryBuilder().WithAll<ShotPendingTag>().Build());
        }

        public void OnUpdate(ref SystemState state)
        {
            // 리스너는 로컬 우주선. 살아 있으면 위치를 갱신하고, 사라지면 마지막 위치를 계속 씀.
            // 로컬 투사체도 GhostOwnerIsLocal 을 가지므로 우주선만 고름.
            // SystemAPI 순회라 LocalTransform 을 쓰는 작업(LocalProjectileSystem)이 끝난 뒤 읽음.
            foreach (var ship in SystemAPI.Query<RefRO<LocalTransform>>()
                         .WithAll<GhostOwnerIsLocal, PlayerSpaceshipMovementComponent>())
            {
                lastListener = ship.ValueRO.Position.xy;
                hasLastListener = true;
            }

            // 리스폰 대기 중에는 리스너가 없는 것으로 다뤄 전부 컬링함.
            // 자기 폭발음은 Dead 가 켜지기 전 프레임에 만들어지므로 이 분기에 걸리지 않음.
            SystemAPI.TryGetSingleton(out PlayerStateComponent playerState);

            bool hasListener = hasLastListener && !playerState.Dead;
            float2 listener = lastListener;

            NetworkTick interpolationTick = SystemAPI.GetSingleton<NetworkTime>().InterpolationTick;
            ComponentLookup<EquippedTurretComponent> equippedLookup = SystemAPI.GetComponentLookup<EquippedTurretComponent>(true);
            ComponentLookup<LocalLaunchComponent> launchLookup = SystemAPI.GetComponentLookup<LocalLaunchComponent>(true);
            ComponentLookup<GhostInstance> ghostLookup = SystemAPI.GetComponentLookup<GhostInstance>(true);

            Entity registryEntity = SystemAPI.GetSingletonEntity<ItemRegistryElement>();
            DynamicBuffer<ItemRegistryElement> registry = SystemAPI.GetBuffer<ItemRegistryElement>(registryEntity);

            var played = new NativeHashSet<ShotSoundKey>(16, Allocator.Temp);
            var ecb = new EntityCommandBuffer(Allocator.Temp);

            // 파티클에 속도를 넣으려면 스폰 직후 Companion 에 접근해야 함. 순회 뒤에 스폰함.
            var effects = new NativeList<ShotEffectSpawn>(Allocator.Temp);

            foreach (
                var (shared, transform, pending, entity) in
                SystemAPI.Query<
                    RefRO<SharedProjectileComponent>,
                    RefRO<LocalTransform>,
                    EnabledRefRW<ShotPendingTag>
                >().WithEntityAccess()
            ) {
                pending.ValueRW = false;

                // 예측 스폰(로컬 발사)과 Ghost 가 아닌 투사체는 항상 새 발사임.
                NetworkTick spawnTick = ghostLookup.TryGetComponent(entity, out GhostInstance ghost)
                    ? ghost.spawnTick
                    : NetworkTick.Invalid;

                if (!SystemAPI.HasComponent<PredictedGhost>(entity)
                    && interpolationTick.IsValid
                    && spawnTick.IsValid
                    && interpolationTick.TicksSince(spawnTick) > MaxShotAgeTicks)
                {
                    continue;
                }

                // 쏜 우주선이 복제 범위 밖이거나 이미 사라짐.
                Entity owner = shared.ValueRO.Owner;

                if (!equippedLookup.HasComponent(owner))
                {
                    continue;
                }

                // 투사체가 처음 보이는 자리가 총구. 프리팹의 크기는 그대로 쓰고 위치/회전만 투사체를 따름.
                Entity shotEffect = shared.ValueRO.ShotEffect;

                if (shotEffect != Entity.Null)
                {
                    LocalTransform effectTransform = SystemAPI.GetComponent<LocalTransform>(shotEffect);
                    effectTransform.Position = transform.ValueRO.Position;
                    effectTransform.Rotation = transform.ValueRO.Rotation;

                    // 발사 방향과 같은 쪽으로 움직일 때만 물려줌. 앞으로 날며 쏘면 전부, 옆/뒤로 움직이면 0.
                    float2 shipVelocity = launchLookup[owner].Velocity;
                    float alignment = math.saturate(math.dot(math.normalizesafe(shipVelocity), transform.ValueRO.Up().xy));

                    effects.Add(new ShotEffectSpawn
                    {
                        Prefab = shotEffect,
                        Transform = effectTransform,
                        Velocity = shipVelocity * alignment,
                    });
                }

                ItemRegistryElement turret = registry.Get(shared.ValueRO.Turret);

                // 반복음이 있는 포탑은 투사체마다 소리를 내지 않음. 반복음은 SoundEffectPlayer 가 냄.
                if (!turret.LoopSound.IsEmpty)
                {
                    continue;
                }

                // 들릴 수 없는 거리면 메시지를 만들지 않음.
                if (!SoundAudibleRange.IsAudible(hasListener, listener, transform.ValueRO.Position.xy))
                {
                    continue;
                }

                FixedString64Bytes clip = turret.Turret.ShotSound;

                if (clip.IsEmpty || !played.Add(new ShotSoundKey { Owner = owner, Clip = clip }))
                {
                    continue;
                }

                ecb.AddComponent(ecb.CreateEntity(), new SoundMessage
                {
                    SoundKey = clip,
                    Position = transform.ValueRO.Position.xy,
                });
            }

            ecb.Playback(state.EntityManager);
            ecb.Dispose();
            played.Dispose();

            EntityManager entityManager = state.EntityManager;

            foreach (ShotEffectSpawn spawn in effects)
            {
                Entity effect = entityManager.Instantiate(spawn.Prefab);
                entityManager.SetComponentData(effect, spawn.Transform);

                // 첫 방출 전에 넣음.
                var velocity = new Vector3(spawn.Velocity.x, spawn.Velocity.y, 0.0F);

                foreach (EffectPartElement part in entityManager.GetBuffer<EffectPartElement>(effect))
                {
                    if (entityManager.HasComponent<CompanionComponent<ParticleSystem>>(part.Value))
                    {
                        ParticleSystem.MainModule main = entityManager.GetCompanion<ParticleSystem>(part.Value).main;
                        main.emitterVelocityMode = ParticleSystemEmitterVelocityMode.Custom;
                        main.emitterVelocity = velocity;
                    }
                }
            }

            effects.Dispose();
        }

        private struct ShotEffectSpawn
        {
            public Entity Prefab;
            public LocalTransform Transform;
            public float2 Velocity;
        }

        private struct ShotSoundKey : IEquatable<ShotSoundKey>
        {
            public Entity Owner;
            public FixedString64Bytes Clip;

            public bool Equals(ShotSoundKey other) => Owner == other.Owner && Clip == other.Clip;

            public override int GetHashCode() => (int)math.hash(new int2(Owner.GetHashCode(), Clip.GetHashCode()));
        }
    }
}
