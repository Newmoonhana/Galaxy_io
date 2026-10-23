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
    /// 서버가 복제한 투사체 명중 수가 늘어나면 마지막 명중 지점에 명중 연출/효과음을 스폰하는 시스템.
    /// 연출은 투사체 방향으로 돌림. 효과음은 연출의 효과음, 없으면 포탑의 명중음.
    /// 로컬 플레이어가 쏜 투사체는 효과음을 내지 않음. 서버의 DamageRPC 로 더 크게 재생함(DamagePopupView).
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
                if (shared.ValueRO.PresentedHitCount == shared.ValueRO.HitCount)
                {
                    continue;
                }

                shared.ValueRW.PresentedHitCount = shared.ValueRO.HitCount;

                Entity prefab = shared.ValueRO.SpawnOnHit;
                float2 position = shared.ValueRO.HitPosition;
                FixedString64Bytes sound = default;

                if (prefab != Entity.Null)
                {
                    // 프리팹의 크기는 그대로 쓰고 위치와 투사체 방향만 바꿈.
                    LocalTransform effectTransform = SystemAPI.GetComponent<LocalTransform>(prefab);
                    effectTransform.Position = new float3(position, 0.0F);
                    effectTransform.Rotation = math.mul(transform.ValueRO.Rotation, effectTransform.Rotation);

                    Entity effect = ecb.Instantiate(prefab);
                    ecb.SetComponent(effect, effectTransform);

                    if (SystemAPI.HasComponent<EffectSoundComponent>(prefab))
                    {
                        sound = SystemAPI.GetComponent<EffectSoundComponent>(prefab).SoundKey;
                    }
                }

                if (sound.IsEmpty && registry.IsCreated)
                {
                    sound = registry.Get(shared.ValueRO.Turret).HitSound;
                }

                // 쏜 우주선 또는 함재기의 주인이 로컬 플레이어인지
                Entity owner = shared.ValueRO.Owner;
                bool local = SystemAPI.HasComponent<CarriedCraftComponent>(owner)
                    ? SystemAPI.GetComponent<CarriedCraftComponent>(owner).OwnerNetworkId == localId.Value
                    : SystemAPI.HasComponent<GhostOwner>(owner) && SystemAPI.GetComponent<GhostOwner>(owner).NetworkId == localId.Value;

                if (!sound.IsEmpty && !local && SoundAudibleRange.IsAudible(hasListener, listener, position))
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
