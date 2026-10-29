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
    /// 서버가 보낸 <see cref="ProjectileShotRPC"/>로 Ghost 가 아닌 투사체를 쏘는 시스템.
    /// 보간 틱이 발사 틱에 닿을 때 쏨. 보간으로 그려지는 우주선의 총구와 맞음.
    /// 늦게 닿았으면 지난 시간만큼 앞으로 옮기고 수명을 줄임.
    /// 쏜 투사체는 <see cref="LocalProjectileSystem"/>이 프레임마다 옮김.
    /// </summary>
    [BurstCompile]
    [WorldSystemFilter(WorldSystemFilterFlags.ClientSimulation)]
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateAfter(typeof(LocalProjectileSystem))]
    [UpdateBefore(typeof(TransformSystemGroup))]
    public partial struct ProjectileShotReceiveSystem : ISystem
    {
        // 발사를 기다리는 최대 시간(초). 보간 지연보다 충분히 김.
        private const float MaxWaitTime = 1.0F;

        // 보간 틱이 닿기를 기다리는 발사
        private NativeList<ProjectileShotRPC> pending;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            pending = new NativeList<ProjectileShotRPC>(64, Allocator.Persistent);

            state.RequireForUpdate<NetworkTime>();
            state.RequireForUpdate<ClientServerTickRate>();
            state.RequireForUpdate<ItemRegistryElement>();
        }

        [BurstCompile]
        public void OnDestroy(ref SystemState state)
        {
            pending.Dispose();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var ecb = new EntityCommandBuffer(Allocator.Temp);

            foreach (
                var (shot, rpcEntity) in
                SystemAPI.Query<RefRO<ProjectileShotRPC>>()
                    .WithAll<ReceiveRpcCommandRequest>()
                    .WithEntityAccess()
            ) {
                pending.Add(shot.ValueRO);
                ecb.DestroyEntity(rpcEntity);
            }

            NetworkTime networkTime = SystemAPI.GetSingleton<NetworkTime>();

            if (pending.IsEmpty || !networkTime.InterpolationTick.IsValid)
            {
                ecb.Playback(state.EntityManager);
                ecb.Dispose();
                return;
            }

            float tickTime = 1.0F / SystemAPI.GetSingleton<ClientServerTickRate>().SimulationTickRate;

            // 자기 우주선은 보간 틱보다 앞선 예측 틱에 그려짐. 두 틱 사이에 우주선이 움직이는 거리.
            // 자동 포탑 투사체를 그만큼 옮겨 총구에 맞춤. 조종 포탑 투사체는 서버가 보내지 않음.
            float lag = networkTime.ServerTick.IsValid
                ? math.max(networkTime.ServerTick.TicksSince(networkTime.InterpolationTick), 0) * tickTime
                : 0.0F;

            DynamicBuffer<ItemRegistryElement> registry = SystemAPI.GetSingletonBuffer<ItemRegistryElement>(true);

            for (int i = pending.Length - 1; i >= 0; i--)
            {
                ProjectileShotRPC shot = pending[i];

                // 보간 시점은 (InterpolationTick - 1 + Fraction). 총구에 놓인 뒤 지난 시간.
                float elapsed = (networkTime.InterpolationTick.TicksSince(new NetworkTick { SerializedData = shot.Tick })
                                 - 1 + networkTime.InterpolationTickFraction) * tickTime;

                // 너무 먼 미래의 발사(재접속 등으로 틱이 어긋남)는 버림.
                if (elapsed < 0.0F && elapsed > -MaxWaitTime)
                {
                    continue;
                }

                pending.RemoveAtSwapBack(i);

                if (elapsed < 0.0F)
                {
                    continue;
                }

                Entity prefab = registry.Get(shot.Turret).Turret.ProjectilePrefab;

                if (prefab == Entity.Null)
                {
                    continue;
                }

                var shared = SystemAPI.GetComponent<SharedProjectileComponent>(prefab);
                shared.Lifetime -= elapsed;

                if (shared.Lifetime <= 0.0F)
                {
                    continue;
                }

                shared.Owner = shot.Owner;
                shared.Turret = shot.Turret;

                LocalTransform transform = SystemAPI.GetComponent<LocalTransform>(prefab);
                transform.Rotation = quaternion.RotateZ(shot.Angle);

                float2 position = shot.Position + transform.Up().xy * math.max(shot.Speed, 0.0F) * elapsed;

                if (SystemAPI.HasComponent<GhostOwnerIsLocal>(shot.Owner)
                    && SystemAPI.IsComponentEnabled<GhostOwnerIsLocal>(shot.Owner)
                    && SystemAPI.HasComponent<MovementComponent>(shot.Owner))
                {
                    position += SystemAPI.GetComponent<MovementComponent>(shot.Owner).Velocity * lag;
                }

                transform.Position = new float3(position, transform.Position.z);

                Entity projectile = ecb.Instantiate(prefab);
                ecb.SetComponent(projectile, transform);
                ecb.SetComponent(projectile, shared);
                ecb.SetComponent(projectile, new FactionComponent { Value = shot.Faction, Party = shot.Party });
                ecb.AddComponent<LocalProjectileTag>(projectile);

                if (SystemAPI.HasComponent<StraightforwardProjectileComponent>(prefab))
                {
                    var launched = SystemAPI.GetComponent<StraightforwardProjectileComponent>(prefab);
                    launched.Speed = shot.Speed;
                    ecb.SetComponent(projectile, launched);
                }
            }

            ecb.Playback(state.EntityManager);
            ecb.Dispose();
        }
    }
}
