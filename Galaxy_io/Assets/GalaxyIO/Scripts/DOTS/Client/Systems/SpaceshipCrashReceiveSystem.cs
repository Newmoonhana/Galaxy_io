using GalaxyIO.DOTS.Client.Components.Message;
using GalaxyIO.DOTS.Client.Components.Singleton;
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
    /// 우주선 충돌 알림을 받아 충돌음을 요청하는 시스템.
    /// </summary>
    [BurstCompile]
    [WorldSystemFilter(WorldSystemFilterFlags.ClientSimulation)]
    public partial struct SpaceshipCrashReceiveSystem : ISystem
    {
        private EntityQuery localShipQuery;

        // 마지막 리스너 위치. 파괴되는 순간의 자기 폭발음을 살리는 데 씀.
        // 리스폰 대기 중(Dead)에는 이 캐시를 쓰지 않음 - 사망 지점 주변 소리까지 들리면 안 됨.
        private float2 lastListener;
        private bool hasLastListener;
        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<CollisionConfigComponent>();
            state.RequireForUpdate<SpaceshipCrashedRPC>();

            localShipQuery = SystemAPI.QueryBuilder().WithAll<GhostOwnerIsLocal, LocalTransform>().Build();
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

            CollisionConfigComponent config = SystemAPI.GetSingleton<CollisionConfigComponent>();

            var ecb = new EntityCommandBuffer(Allocator.Temp);

            foreach (
                var (crash, rpcEntity) in
                SystemAPI.Query<RefRO<SpaceshipCrashedRPC>>()
                    .WithAll<ReceiveRpcCommandRequest>()
                    .WithEntityAccess()
            ) {
                if (config.CrashSound != default
                    && SoundAudibleRange.IsAudible(hasListener, listener, crash.ValueRO.Position))
                {
                    ecb.AddComponent(ecb.CreateEntity(), new SoundMessage
                    {
                        SoundKey = config.CrashSound,
                        Position = crash.ValueRO.Position,
                    });
                }

                ecb.DestroyEntity(rpcEntity);
            }

            ecb.Playback(state.EntityManager);
            ecb.Dispose();
        }
    }
}
