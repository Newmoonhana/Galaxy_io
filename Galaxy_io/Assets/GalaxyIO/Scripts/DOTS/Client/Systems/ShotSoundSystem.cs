using System;
using GalaxyIO.DOTS.Client.Components.Message;
using GalaxyIO.DOTS.Components.Actor;
using GalaxyIO.DOTS.Components.Singleton;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.NetCode;
using Unity.Transforms;
using UnityEngine;

namespace GalaxyIO.DOTS.Client.Systems
{
    /// <summary>
    /// 새로 나타난 투사체의 발사음을 요청하는 시스템.
    /// 한 우주선의 같은 발사음은 프레임당 한 번만 재생함.
    /// </summary>
    [BurstCompile]
    [WorldSystemFilter(WorldSystemFilterFlags.ClientSimulation)]
    public partial struct ShotSoundSystem : ISystem
    {
        // 이보다 오래된 투사체는 발사가 아니라 복제 범위에 새로 들어온 것으로 봄.
        private const int MaxShotAgeTicks = 6;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<NetworkTime>();
            state.RequireForUpdate<ItemRegistryElement>();
            state.RequireForUpdate(SystemAPI.QueryBuilder().WithAll<ShotSoundPendingTag>().Build());
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            NetworkTick interpolationTick = SystemAPI.GetSingleton<NetworkTime>().InterpolationTick;
            ComponentLookup<EquippedTurretComponent> equippedLookup = SystemAPI.GetComponentLookup<EquippedTurretComponent>(true);

            Entity registryEntity = SystemAPI.GetSingletonEntity<ItemRegistryElement>();
            DynamicBuffer<ItemRegistryElement> registry = SystemAPI.GetBuffer<ItemRegistryElement>(registryEntity);
            DynamicBuffer<TurretSpecElement> specs = SystemAPI.GetBuffer<TurretSpecElement>(registryEntity);

            var played = new NativeHashSet<ShotSoundKey>(16, Allocator.Temp);
            var ecb = new EntityCommandBuffer(Allocator.Temp);

            foreach (
                var (shared, transform, ghost, pending, entity) in
                SystemAPI.Query<
                    RefRO<SharedProjectileComponent>,
                    RefRO<LocalTransform>,
                    RefRO<GhostInstance>,
                    EnabledRefRW<ShotSoundPendingTag>
                >().WithEntityAccess()
            ) {
                pending.ValueRW = false;

                // 예측 스폰(로컬 발사)은 항상 새 발사임.
                if (!SystemAPI.HasComponent<PredictedGhost>(entity)
                    && interpolationTick.IsValid
                    && ghost.ValueRO.spawnTick.IsValid
                    && interpolationTick.TicksSince(ghost.ValueRO.spawnTick) > MaxShotAgeTicks)
                {
                    continue;
                }

                // 쏜 우주선이 복제 범위 밖이거나 이미 사라짐.
                Entity owner = shared.ValueRO.Owner;

                if (!equippedLookup.TryGetComponent(owner, out EquippedTurretComponent equipped))
                {
                    continue;
                }

                // 반복음이 있는 포탑은 투사체마다 소리를 내지 않음. 반복음은 SoundEffectPlayer 가 냄.
                if (registry.Get(equipped.Value).LoopSound != default)
                {
                    continue;
                }

                UnityObjectRef<AudioClip> clip =
                    registry.GetTurretSpec(specs, equipped.Value, shared.ValueRO.TurretIndex).ShotSound;

                if (clip == default || !played.Add(new ShotSoundKey { Owner = owner, Clip = clip }))
                {
                    continue;
                }

                ecb.AddComponent(ecb.CreateEntity(), new SoundMessage
                {
                    Clip     = clip,
                    Position = transform.ValueRO.Position.xy,
                });
            }

            ecb.Playback(state.EntityManager);
            ecb.Dispose();
            played.Dispose();
        }

        private struct ShotSoundKey : IEquatable<ShotSoundKey>
        {
            public Entity Owner;
            public UnityObjectRef<AudioClip> Clip;

            public bool Equals(ShotSoundKey other) => Owner == other.Owner && Clip == other.Clip;

            public override int GetHashCode() => (int)math.hash(new int2(Owner.GetHashCode(), Clip.GetHashCode()));
        }
    }
}
