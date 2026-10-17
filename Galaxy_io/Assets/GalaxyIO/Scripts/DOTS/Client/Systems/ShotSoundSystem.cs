using System;
using GalaxyIO.DOTS.Client.Components.Message;
using GalaxyIO.DOTS.Client.Components.Singleton;
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

        private EntityQuery localShipQuery;

        // 마지막 리스너 위치. 파괴되는 순간의 자기 폭발음을 살리는 데 씀.
        // 리스폰 대기 중(Dead)에는 이 캐시를 쓰지 않음 - 사망 지점 주변 소리까지 들리면 안 됨.
        private float2 lastListener;
        private bool hasLastListener;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<NetworkTime>();
            state.RequireForUpdate<ItemRegistryElement>();
            state.RequireForUpdate(SystemAPI.QueryBuilder().WithAll<ShotSoundPendingTag>().Build());

            // 로컬 투사체도 GhostOwnerIsLocal 을 가지므로 우주선만 고름.
            localShipQuery = SystemAPI.QueryBuilder()
                .WithAll<GhostOwnerIsLocal, PlayerSpaceshipMovementComponent, LocalTransform>()
                .Build();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            // 리스너는 로컬 우주선. 살아 있으면 위치를 갱신하고, 사라지면 마지막 위치를 계속 씀.
            if (!localShipQuery.IsEmpty)
            {
                lastListener = localShipQuery.GetSingleton<LocalTransform>().Position.xy;
                hasLastListener = true;
            }

            // 리스폰 대기 중에는 리스너가 없는 것으로 다뤄 전부 컬링함.
            // 자기 폭발음은 Dead 가 켜지기 전 프레임에 만들어지므로 이 분기에 걸리지 않음.
            SystemAPI.TryGetSingleton(out PlayerStateComponent playerState);

            bool hasListener = hasLastListener && !playerState.Dead;
            float2 listener = lastListener;

            NetworkTick interpolationTick = SystemAPI.GetSingleton<NetworkTime>().InterpolationTick;
            ComponentLookup<EquippedTurretComponent> equippedLookup = SystemAPI.GetComponentLookup<EquippedTurretComponent>(true);

            Entity registryEntity = SystemAPI.GetSingletonEntity<ItemRegistryElement>();
            DynamicBuffer<ItemRegistryElement> registry = SystemAPI.GetBuffer<ItemRegistryElement>(registryEntity);

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

                if (!equippedLookup.HasComponent(owner))
                {
                    continue;
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
