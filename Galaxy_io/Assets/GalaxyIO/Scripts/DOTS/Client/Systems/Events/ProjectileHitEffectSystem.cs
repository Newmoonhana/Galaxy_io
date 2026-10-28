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

namespace GalaxyIO.DOTS.Client.Systems
{
    /// <summary>
    /// 서버가 복제한 투사체 명중 수가 늘어나면 마지막 명중 지점에 명중 연출을 스폰하는 시스템.
    /// 연출은 투사체 방향으로 돌림.
    /// 명중음은 연출의 효과음, 없으면 포탑의 명중음. 포탑의 <see cref="HitSoundMode"/>에 따라
    /// 클라이언트가 판정한 명중이나 서버가 판정한 명중에서 냄.
    /// 로컬 플레이어의 명중음은 DamagePopupView 가 더 크게 재생함. 예측 명중은 <see cref="AttackerHitSoundMessage"/>로,
    /// 서버 명중은 DamageRPC 로 받음.
    /// </summary>
    [BurstCompile]
    [WorldSystemFilter(WorldSystemFilterFlags.ClientSimulation)]
    public partial struct ProjectileHitEffectSystem : ISystem
    {
        // 마지막 리스너 위치. 파괴되는 순간의 자기 폭발음을 살리는 데 씀.
        // 리스폰 대기 중(Dead)에는 이 캐시를 쓰지 않음 - 사망 지점 주변 소리까지 들리면 안 됨.
        private float2 lastListener;
        private bool hasLastListener;
        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate(SystemAPI.QueryBuilder().WithAll<SharedProjectileComponent>().Build());
        }

        [BurstCompile]
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

            SystemAPI.TryGetSingleton(out NetworkId localId);
            SystemAPI.TryGetSingletonBuffer(out DynamicBuffer<ItemRegistryElement> registry, true);

            var ecb = new EntityCommandBuffer(Allocator.Temp);

            foreach (var (shared, transform) in SystemAPI.Query<RefRW<SharedProjectileComponent>, RefRO<LocalTransform>>())
            {
                bool hit = shared.ValueRO.PresentedHitCount != shared.ValueRO.HitCount;
                bool predictedHit = shared.ValueRO.PresentedPredictedHitCount != shared.ValueRO.PredictedHitCount;

                if (!hit && !predictedHit)
                {
                    continue;
                }

                shared.ValueRW.PresentedHitCount = shared.ValueRO.HitCount;
                shared.ValueRW.PresentedPredictedHitCount = shared.ValueRO.PredictedHitCount;

                Entity prefab = shared.ValueRO.SpawnOnHit;

                if (hit && prefab != Entity.Null)
                {
                    // 프리팹의 크기는 그대로 쓰고 위치와 투사체 방향만 바꿈.
                    LocalTransform effectTransform = SystemAPI.GetComponent<LocalTransform>(prefab);
                    effectTransform.Position = new float3(shared.ValueRO.HitPosition, 0.0F);
                    effectTransform.Rotation = math.mul(transform.ValueRO.Rotation, effectTransform.Rotation);

                    Entity effect = ecb.Instantiate(prefab);
                    ecb.SetComponent(effect, effectTransform);
                }

                ItemRegistryElement turret = registry.IsCreated ? registry.Get(shared.ValueRO.Turret) : default;

                FixedString64Bytes sound = prefab != Entity.Null && SystemAPI.HasComponent<EffectSoundComponent>(prefab)
                    ? SystemAPI.GetComponent<EffectSoundComponent>(prefab).SoundKey
                    : turret.HitSound;

                // 포탑이 정한 쪽의 명중에서만 소리를 냄.
                bool predicted = turret.HitSoundMode == HitSoundMode.Predicted;

                if (sound.IsEmpty || (predicted ? !predictedHit : !hit))
                {
                    continue;
                }

                float2 position = predicted ? shared.ValueRO.PredictedHitPosition : shared.ValueRO.HitPosition;

                // 쏜 우주선 또는 함재기의 주인이 로컬 플레이어인지
                Entity owner = shared.ValueRO.Owner;
                bool local = SystemAPI.HasComponent<CarriedCraftComponent>(owner)
                    ? SystemAPI.GetComponent<CarriedCraftComponent>(owner).OwnerNetworkId == localId.Value
                    : SystemAPI.HasComponent<GhostOwner>(owner) && SystemAPI.GetComponent<GhostOwner>(owner).NetworkId == localId.Value;

                // 로컬 플레이어의 서버 명중음은 DamageRPC 로 냄.
                if (local)
                {
                    if (predicted)
                    {
                        ecb.AddComponent(ecb.CreateEntity(), new AttackerHitSoundMessage
                        {
                            SoundKey = sound,
                            Position = position,
                        });
                    }
                }
                else if (SoundAudibleRange.IsAudible(hasListener, listener, position))
                {
                    ecb.AddComponent(ecb.CreateEntity(), new SoundMessage
                    {
                        SoundKey = sound,
                        Position = position,
                    });
                }
            }

            ecb.Playback(state.EntityManager);
            ecb.Dispose();
        }
    }
}
