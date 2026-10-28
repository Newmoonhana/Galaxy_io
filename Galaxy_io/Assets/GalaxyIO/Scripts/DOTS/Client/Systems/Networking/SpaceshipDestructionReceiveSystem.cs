using GalaxyIO.DOTS.Client.Components.Message;
using GalaxyIO.DOTS.Client.Components.Singleton;
using GalaxyIO.DOTS.Components.Actor;
using GalaxyIO.DOTS.Components.Message;
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
    /// 우주선 파괴 알림을 받아 폭발 연출/효과음을 스폰하고 리스폰 창 상태를 켜는 시스템.
    /// 로컬 플레이어가 막타를 쳤으면 폭발음을 <see cref="AttackerHitSoundMessage"/>로 보내 멀어도 들리게 함.
    /// </summary>
    [BurstCompile]
    [WorldSystemFilter(WorldSystemFilterFlags.ClientSimulation)]
    public partial struct SpaceshipDestructionReceiveSystem : ISystem
    {
        // 마지막 리스너 위치. 파괴되는 순간의 자기 폭발음을 살리는 데 씀.
        // 리스폰 대기 중(Dead)에는 이 캐시를 쓰지 않음 - 사망 지점 주변 소리까지 들리면 안 됨.
        private float2 lastListener;
        private bool hasLastListener;
        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<NetworkId>();
            state.RequireForUpdate<PrefabRegistryComponent>();
            state.RequireForUpdate<SpaceshipDestroyedRPC>();
            state.RequireForUpdate<PlayerStateComponent>();
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

            int localNetworkId = SystemAPI.GetSingleton<NetworkId>().Value;

            Entity explosionPrefab = SystemAPI.GetSingleton<PrefabRegistryComponent>().ExplosionEffect;

            // 프리팹의 회전/크기는 그대로 쓰고 위치만 바꿈.
            LocalTransform explosionTransform = explosionPrefab == Entity.Null
                ? LocalTransform.Identity
                : SystemAPI.GetComponent<LocalTransform>(explosionPrefab);

            bool hasSound = SystemAPI.HasComponent<EffectSoundComponent>(explosionPrefab);
            EffectSoundComponent explosionSound = hasSound
                ? SystemAPI.GetComponent<EffectSoundComponent>(explosionPrefab)
                : default;

            var ecb = new EntityCommandBuffer(Allocator.Temp);

            foreach (
                var (destroyed, rpcEntity) in
                SystemAPI.Query<RefRO<SpaceshipDestroyedRPC>>()
                    .WithAll<ReceiveRpcCommandRequest>()
                    .WithEntityAccess()
            ) {
                // 레지스트리에 연출이 없어도 리스폰 처리는 계속함.
                if (explosionPrefab != Entity.Null)
                {
                    Entity explosion = ecb.Instantiate(explosionPrefab);

                    explosionTransform.Position = new float3(destroyed.ValueRO.Position, 0.0F);
                    ecb.SetComponent(explosion, explosionTransform);
                }

                bool killedByLocal = destroyed.ValueRO.KillerNetworkId == localNetworkId
                                     && destroyed.ValueRO.OwnerNetworkId != localNetworkId;

                if (hasSound && killedByLocal)
                {
                    ecb.AddComponent(ecb.CreateEntity(), new AttackerHitSoundMessage
                    {
                        SoundKey = explosionSound.SoundKey,
                        Position = destroyed.ValueRO.Position,
                    });
                }
                else if (hasSound
                    && SoundAudibleRange.IsAudible(hasListener, listener, destroyed.ValueRO.Position))
                {
                    ecb.AddComponent(ecb.CreateEntity(), new SoundMessage
                    {
                        SoundKey = explosionSound.SoundKey,
                        Position = destroyed.ValueRO.Position,
                    });
                }

                if (destroyed.ValueRO.OwnerNetworkId == localNetworkId)
                {
                    SystemAPI.GetSingletonRW<PlayerStateComponent>().ValueRW.Dead = true;
                }

                ecb.DestroyEntity(rpcEntity);
            }

            ecb.Playback(state.EntityManager);
            ecb.Dispose();
        }
    }
}