using GalaxyIO.DOTS.Collision;
using GalaxyIO.DOTS.Components;
using GalaxyIO.DOTS.Components.Collision;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.NetCode;
using Unity.Transforms;

namespace GalaxyIO.DOTS.Systems
{
    /// <summary>
    /// 판정에 쓰려고 한 틱 동안 붙잡아 두는 우주선 정보.
    /// </summary>
    public struct SpaceshipCollider
    {
        public Entity Entity;
        public ColliderWorld Collider;

        // 이 우주선을 조종하는 클라이언트. 투사체와 같으면 자기가 쏜 탄임.
        public int OwnerNetworkId;

        // 정규화된 진행 방향
        public float2 Direction;

        public float Speed;
    }

    /// <summary>
    /// 우주선에 적용할 데미지. 병렬 Job이 다른 Entity에 직접 쓸 수 없어 큐를 거침.
    /// </summary>
    public struct SpaceshipDamage
    {
        public Entity Spaceship;
        public float Amount;
    }

    /// <summary>
    /// 충돌체를 가진 물체끼리의 겹침을 판정하는 시스템.
    /// 투사체 제거는 <see cref="SharedProjectileSystem"/>이 담당하므로 Lifetime만 만료시킴.
    /// </summary>
    [BurstCompile]
    [WorldSystemFilter(WorldSystemFilterFlags.ServerSimulation)]
    [UpdateInGroup(typeof(CollisionDetectionSystemGroup))]
    public partial struct CollisionDetectionSystem : ISystem
    {
        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<NetworkTime>();
            state.RequireForUpdate(
                SystemAPI.QueryBuilder()
                    .WithAll<SpaceshipSpecComponent, SpaceshipMovementComponent, ColliderComponent, LocalTransform>()
                    .Build()
            );
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            NetworkTime networkTime = SystemAPI.GetSingleton<NetworkTime>();

            // 우주선은 수가 적어 매 틱 배열로 모아 전수 비교함.
            // 투사체가 늘어 O(S*P)가 부담되면 이 앞에 브로드페이즈를 넣으면 됨.
            var spaceships = new NativeList<SpaceshipCollider>(Allocator.TempJob);
            var damages = new NativeQueue<SpaceshipDamage>(Allocator.TempJob);

            state.Dependency = new GatherSpaceshipColliderJob
            {
                Spaceships = spaceships,
            }.Schedule(state.Dependency);

            // 지연이 있는 클라이언트의 명중을 되감아 판정하려면 지난 틱 위치가 필요함.
            state.Dependency = new RecordSpaceshipColliderJob
            {
                Tick = networkTime.ServerTick,
            }.ScheduleParallel(state.Dependency);

            state.Dependency = new ProjectileHitSpaceshipJob
            {
                Spaceships  = spaceships,
                Histories   = SystemAPI.GetBufferLookup<SpaceshipColliderHistory>(true),
                CurrentTick = networkTime.ServerTick,
                Damages     = damages.AsParallelWriter(),
            }.ScheduleParallel(state.Dependency);

            JobHandle spaceshipHit = new SpaceshipHitSpaceshipJob
            {
                Spaceships = spaceships,
                Damages = damages.AsParallelWriter(),
            }.ScheduleParallel(state.Dependency);

            state.Dependency = new ApplySpaceshipDamageJob
            {
                Damages = damages,
                Specs = SystemAPI.GetComponentLookup<SpaceshipSpecComponent>(),
            }.Schedule(spaceshipHit);

            state.Dependency = spaceships.Dispose(state.Dependency);
            state.Dependency = damages.Dispose(state.Dependency);
        }
    }

    /// <summary>
    /// 우주선의 충돌체를 월드 좌표로 펼쳐 배열에 모음.
    /// 표적은 예측 대상이 아닌 우주선도 포함해야 하므로 Simulate로 거르지 않음.
    /// </summary>
    [BurstCompile]
    [WithAll(typeof(SpaceshipSpecComponent))]
    public partial struct GatherSpaceshipColliderJob : IJobEntity
    {
        public NativeList<SpaceshipCollider> Spaceships;

        private void Execute(
            Entity entity,
            in LocalTransform transform,
            in ColliderComponent collider,
            in SpaceshipMovementComponent movement,
            in GhostOwner owner)
        {
            Spaceships.Add(new SpaceshipCollider
            {
                Entity         = entity,
                Collider       = ColliderOverlap.ToWorld(collider, transform),
                OwnerNetworkId = owner.NetworkId,
                Direction      = math.normalizesafe(movement.DeltaMovement, float2.zero),
                Speed          = math.length(movement.DeltaMovement),
            });
        }
    }

    /// <summary>
    /// 우주선 충돌체를 현재 틱 칸에 남김. 되감기 판정에 씀.
    /// </summary>
    [BurstCompile]
    [WithAll(typeof(SpaceshipSpecComponent))]
    public partial struct RecordSpaceshipColliderJob : IJobEntity
    {
        public NetworkTick Tick;

        private void Execute(
            DynamicBuffer<SpaceshipColliderHistory> history,
            in LocalTransform transform,
            in ColliderComponent collider)
        {
            // 링을 처음 한 번만 늘림. 빈 칸의 Tick은 0이고, 유효한 틱은 절대 0이 아님.
            if (history.Length != SpaceshipColliderHistory.Length)
            {
                history.Length = SpaceshipColliderHistory.Length;
            }

            history[(int)(Tick.TickIndexForValidTick % SpaceshipColliderHistory.Length)] =
                new SpaceshipColliderHistory
                {
                    Tick     = Tick.SerializedData,
                    Collider = ColliderOverlap.ToWorld(collider, transform),
                };
        }
    }

    /// <summary>
    /// 투사체 &lt;&gt; 우주선. 우주선에 데미지를 주고 투사체를 만료시킴.
    /// 투사체끼리는 쌍을 만들지 않으므로 서로 통과함.
    /// </summary>
    [BurstCompile]
    [WithAll(typeof(Simulate))]
    public partial struct ProjectileHitSpaceshipJob : IJobEntity
    {
        [ReadOnly]
        public NativeList<SpaceshipCollider> Spaceships;

        [ReadOnly]
        public BufferLookup<SpaceshipColliderHistory> Histories;

        public NetworkTick CurrentTick;

        public NativeQueue<SpaceshipDamage>.ParallelWriter Damages;

        private void Execute(
            in LocalTransform transform,
            in ColliderComponent collider,
            ref SharedProjectileComponent projectile)
        {
            // 한 틱 이동량만큼 늘린 충돌체로 판정. 탄속이 표적보다 커도 통과하지 않음.
            ColliderWorld world = ColliderOverlap.Sweep(
                ColliderOverlap.ToWorld(collider, transform),
                projectile.Displacement
            );

            for (int i = 0; i < Spaceships.Length; i++)
            {
                // 자기가 쏜 탄은 통과. 총구가 선체 안에 있어도 자폭하지 않음.
                if (Spaceships[i].Entity == projectile.Owner)
                {
                    continue;
                }

                // 쏜 사람이 화면에서 본 시점의 위치로 되감음. 클라이언트에서는 지연이 0이라 그대로 씀.
                ColliderWorld target = Spaceships[i].Collider;
                TryRewind(Spaceships[i].Entity, projectile.InterpolationDelay, ref target);

                if (!ColliderOverlap.Test(world, target, out _))
                {
                    continue;
                }

                Damages.Enqueue(new SpaceshipDamage
                {
                    Spaceship = Spaceships[i].Entity,
                    Amount    = projectile.Damage,
                });

                // 제거는 SharedProjectileSystem이 함. 여기선 만료만 시킴.
                projectile.Lifetime = 0.0F;
                return;
            }
        }

        /// <summary>
        /// 기록해 둔 지난 틱 충돌체로 바꿔치기함. 기록이 없으면 현재 위치를 그대로 둠.
        /// </summary>
        private void TryRewind(Entity spaceship, uint delay, ref ColliderWorld collider)
        {
            if (delay == 0)
            {
                return;
            }

            NetworkTick tick = CurrentTick;
            tick.Subtract(delay);

            if (!Histories.TryGetBuffer(spaceship, out DynamicBuffer<SpaceshipColliderHistory> history)
                || history.Length != SpaceshipColliderHistory.Length)
            {
                return;
            }

            SpaceshipColliderHistory entry =
                history[(int)(tick.TickIndexForValidTick % SpaceshipColliderHistory.Length)];

            // 링을 한 바퀴 넘겨 덮인 칸은 틱이 맞지 않음. 그때는 되감지 않음.
            if (entry.Tick != tick.SerializedData)
            {
                return;
            }

            collider = entry.Collider;
        }
    }

    /// <summary>
    /// 우주선 &lt;&gt; 우주선. 데미지 식이 대칭이라 각 우주선이 자기 몫만 계산함.
    /// 겹친 깊이만큼 서로 밀어냄.
    /// </summary>
    [BurstCompile]
    [WithAll(typeof(SpaceshipSpecComponent))]
    public partial struct SpaceshipHitSpaceshipJob : IJobEntity
    {
        [ReadOnly]
        public NativeList<SpaceshipCollider> Spaceships;

        public NativeQueue<SpaceshipDamage>.ParallelWriter Damages;

        private void Execute(
            Entity entity,
            ref LocalTransform transform,
            in ColliderComponent collider,
            in SpaceshipMovementComponent movement)
        {
            ColliderWorld world = ColliderOverlap.ToWorld(collider, transform);
            float2 direction = math.normalizesafe(movement.DeltaMovement, float2.zero);
            float speed = math.length(movement.DeltaMovement);
            float2 push = float2.zero;

            for (int i = 0; i < Spaceships.Length; i++)
            {
                if (Spaceships[i].Entity == entity)
                {
                    continue;
                }

                if (!ColliderOverlap.Test(world, Spaceships[i].Collider, out ColliderHit hit))
                {
                    continue;
                }

                // 서로 마주 볼수록 1에 가까워지는 정면 충돌 계수. 같은 방향으로 스치면 0.
                float closing = math.max(0.0F, -math.dot(direction, Spaceships[i].Direction));

                Damages.Enqueue(new SpaceshipDamage
                {
                    Spaceship = entity,
                    Amount    = speed * Spaceships[i].Speed * closing,
                });

                // 깊이의 절반씩 서로 물러나면 딱 맞닿음.
                // 빠를수록 한 틱에 깊이 박히므로 밀려나는 양도 자동으로 커짐.
                push += hit.Normal * (hit.Depth * 0.5F);
            }

            // 판정에 쓴 충돌체 배열은 밀어내기 전 위치라, 계산 순서에 관계없이 결과가 같음.
            transform.Position = new float3(transform.Position.xy + push, transform.Position.z);
        }
    }

    /// <summary>
    /// 쌓인 데미지를 실제 체력에 반영함. 다른 Entity에 쓰므로 단일 스레드로 돎.
    /// </summary>
    [BurstCompile]
    public struct ApplySpaceshipDamageJob : IJob
    {
        public NativeQueue<SpaceshipDamage> Damages;
        public ComponentLookup<SpaceshipSpecComponent> Specs;

        public void Execute()
        {
            while (Damages.TryDequeue(out SpaceshipDamage damage))
            {
                // 스치듯 지나가 데미지가 0인 충돌은 수리 대기를 걸지 않음.
                if (damage.Amount <= 0.0F || !Specs.HasComponent(damage.Spaceship))
                {
                    continue;
                }

                SpaceshipSpecComponent spec = Specs[damage.Spaceship];
                spec.Health = math.max(spec.Health - damage.Amount, 0.0F);
                spec.RepairCooldown = spec.RepairDelay;
                Specs[damage.Spaceship] = spec;
            }
        }
    }
}