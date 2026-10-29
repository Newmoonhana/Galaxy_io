using GalaxyIO.DOTS.Components.Actor;
using GalaxyIO.DOTS.Components.Message;
using GalaxyIO.DOTS.Components.Singleton;
using Unity.Burst;
using Unity.Entities;
using Unity.Mathematics;
using Unity.NetCode;
using Unity.Transforms;

namespace GalaxyIO.DOTS.Systems
{
    /// <summary>
    /// 스폰 메시지를 받아 투사체, 우주선, 함재기, 아이템을 만드는 시스템.
    /// 메시지는 싱글톤 Entity 하나의 버퍼에 모임. 만든 Entity 는 예측 루프가 끝날 때 생김.
    /// 서버는 <see cref="ProjectileReplication.Shot"/> 투사체의 발사 정보를 <see cref="ProjectileShotMessage"/>로 남김.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(SpawnSystemGroup))]
    public partial struct SpawnSystem : ISystem
    {
        private bool isServer;

        public void OnCreate(ref SystemState state)
        {
            isServer = state.WorldUnmanaged.IsServer();

            state.EntityManager.CreateEntity(
                typeof(ProjectileSpawnMessage),
                typeof(ProjectileShotMessage),
                typeof(SpaceshipSpawnMessage),
                typeof(CraftSpawnMessage),
                typeof(ItemSpawnMessage)
            );

            state.RequireForUpdate<ItemRegistryElement>();
            state.RequireForUpdate<EndPredictedSimulationEntityCommandBufferSystem.Singleton>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            Entity messages = SystemAPI.GetSingletonEntity<ProjectileSpawnMessage>();
            DynamicBuffer<ProjectileSpawnMessage> projectiles = SystemAPI.GetBuffer<ProjectileSpawnMessage>(messages);
            DynamicBuffer<SpaceshipSpawnMessage> spaceships = SystemAPI.GetBuffer<SpaceshipSpawnMessage>(messages);
            DynamicBuffer<CraftSpawnMessage> crafts = SystemAPI.GetBuffer<CraftSpawnMessage>(messages);
            DynamicBuffer<ItemSpawnMessage> items = SystemAPI.GetBuffer<ItemSpawnMessage>(messages);

            if (projectiles.IsEmpty && spaceships.IsEmpty && crafts.IsEmpty && items.IsEmpty)
            {
                return;
            }

            EntityCommandBuffer ecb = SystemAPI
                .GetSingleton<EndPredictedSimulationEntityCommandBufferSystem.Singleton>()
                .CreateCommandBuffer(state.WorldUnmanaged);

            DynamicBuffer<ItemRegistryElement> registry = SystemAPI.GetSingletonBuffer<ItemRegistryElement>(true);
            DynamicBuffer<ProjectileShotMessage> shots = SystemAPI.GetBuffer<ProjectileShotMessage>(messages);

            foreach (ProjectileSpawnMessage message in projectiles)
            {
                // 같은 틱에 사라진 우주선의 사격은 버림.
                if (!SystemAPI.HasComponent<LocalTransform>(message.Spawner))
                {
                    continue;
                }

                LocalTransform spawner = SystemAPI.GetComponent<LocalTransform>(message.Spawner);
                LocalTransform transform = spawner.TransformTransform(message.Muzzle);
                FactionComponent faction = SystemAPI.GetComponent<FactionComponent>(message.Spawner);

                // 나머지 값은 프리팹 베이킹 결과를 그대로 씀.
                var shared = SystemAPI.GetComponent<SharedProjectileComponent>(message.Prefab);
                shared.Owner = message.Spawner;
                shared.Turret = message.Turret;
                shared.Damage = message.Damage;
                shared.InterpolationDelay = message.InterpolationDelay;

                Entity projectile = ecb.Instantiate(message.Prefab);
                ecb.SetComponent(projectile, transform);
                ecb.SetComponent(projectile, new GhostOwner { NetworkId = message.NetworkId });
                ecb.SetComponent(projectile, new ProjectileSpawnIdComponent { Value = message.SpawnId });

                // 쏜 우주선의 진영을 물려받음.
                ecb.SetComponent(projectile, faction);
                ecb.SetComponent(projectile, shared);

                float speed = 0.0F;
                float acceleration = 0.0F;

                // 직진 투사체는 우주선 속도의 InheritAxis 방향 성분을 물려받음.
                if (SystemAPI.HasComponent<StraightforwardProjectileComponent>(message.Prefab))
                {
                    var launched = SystemAPI.GetComponent<StraightforwardProjectileComponent>(message.Prefab);
                    float2 axis = math.mul(spawner.Rotation, new float3(message.InheritAxis, 0.0F)).xy;

                    launched.Speed = message.Speed + (launched.ReflectOwnerVelocity
                        ? math.dot(SystemAPI.GetComponent<MovementComponent>(message.Spawner).Velocity, axis)
                        : 0.0F);
                    ecb.SetComponent(projectile, launched);

                    speed = launched.Speed;
                    acceleration = launched.Acceleration;
                }

                if (registry.Get(message.Turret).Turret.Replication != ProjectileReplication.Shot)
                {
                    continue;
                }

                // 클라이언트가 직접 쏜 투사체. 롤백 재시뮬레이션에서 빠지고 LocalProjectileSystem 이 프레임마다 옮김.
                if (!isServer)
                {
                    ecb.AddComponent<LocalProjectileTag>(projectile);
                    continue;
                }

                float2 up = transform.Up().xy;
                float lifetime = shared.Lifetime;

                shots.Add(new ProjectileShotMessage
                {
                    Shot = new ProjectileShotRPC
                    {
                        Owner    = message.Spawner,
                        Turret   = message.Turret,
                        Faction  = faction.Value,
                        Party    = faction.Party,
                        Position = transform.Position.xy,
                        Angle    = math.atan2(-up.x, up.y),
                        Speed    = speed,
                    },
                    NetworkId = message.NetworkId,
                    Reach     = math.max(speed * lifetime + 0.5F * acceleration * lifetime * lifetime, 0.0F),
                });
            }

            foreach (SpaceshipSpawnMessage message in spaceships)
            {
                // 크기는 프리팹 값을 씀.
                LocalTransform transform = SystemAPI.GetComponent<LocalTransform>(message.Prefab);
                transform.Position = message.Position;
                transform.Rotation = message.Rotation;

                Entity spaceship = ecb.Instantiate(message.Prefab);
                ecb.SetComponent(spaceship, transform);
                ecb.SetComponent(spaceship, new GhostOwner { NetworkId = message.NetworkId });
            }

            foreach (CraftSpawnMessage message in crafts)
            {
                if (!SystemAPI.HasComponent<LocalTransform>(message.Carrier))
                {
                    continue;
                }

                // 크기는 프리팹 값, 위치와 회전은 모선 값.
                LocalTransform carrier = SystemAPI.GetComponent<LocalTransform>(message.Carrier);
                LocalTransform transform = SystemAPI.GetComponent<LocalTransform>(message.Prefab);
                transform.Position = carrier.Position;
                transform.Rotation = carrier.Rotation;

                SpaceshipSpecComponent health = SystemAPI.GetComponent<SpaceshipSpecComponent>(message.Prefab);
                health.MaxHealth += message.HealthBonus;
                health.Health = health.MaxHealth;

                Entity craft = ecb.Instantiate(message.Prefab);
                ecb.SetComponent(craft, transform);

                // 적 우주선과 같은 소유자 -1. 주인 화면에서도 보간으로 그림.
                ecb.SetComponent(craft, new GhostOwner { NetworkId = -1 });
                ecb.SetComponent(craft, SystemAPI.GetComponent<FactionComponent>(message.Carrier));
                ecb.SetComponent(craft, health);
                ecb.SetComponent(craft, new MovementComponent
                {
                    Velocity = SystemAPI.GetComponent<MovementComponent>(message.Carrier).Velocity,
                });
                ecb.SetComponent(craft, message.Craft);
            }

            foreach (ItemSpawnMessage message in items)
            {
                if (!registry.Contains(message.Id))
                {
                    continue;
                }

                // 프리팹의 설정값과 회전/크기는 그대로 쓰고 아이템 종류, 속도, 위치만 바꿈.
                ItemComponent item = SystemAPI.GetComponent<ItemComponent>(message.Prefab);
                item.Id = message.Id;
                item.Count = message.Count;
                item.Launch(message.Velocity, message.PickupDelay);

                ColliderComponent collider = SystemAPI.GetComponent<ColliderComponent>(message.Prefab);
                collider.Extents = registry[(int)message.Id].HalfExtents;

                LocalTransform transform = SystemAPI.GetComponent<LocalTransform>(message.Prefab);
                transform.Position = message.Position;

                Entity entity = ecb.Instantiate(message.Prefab);
                ecb.SetComponent(entity, item);
                ecb.SetComponent(entity, collider);
                ecb.SetComponent(entity, transform);
            }

            projectiles.Clear();
            spaceships.Clear();
            crafts.Clear();
            items.Clear();
        }
    }
}
