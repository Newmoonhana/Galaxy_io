using GalaxyIO.DOTS.Collision;
using GalaxyIO.DOTS.Components.Actor;
using GalaxyIO.DOTS.Components.Singleton;
using GalaxyIO.DOTS.Spaceship;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Transforms;

namespace GalaxyIO.DOTS.Systems
{
    /// <summary>
    /// 우주선끼리 부딪힌 결과를 반영하는 시스템. 겹친 깊이만큼 밀어내고 데미지를 줌.
    /// </summary>
    [BurstCompile]
    [WorldSystemFilter(WorldSystemFilterFlags.ServerSimulation)]
    [UpdateInGroup(typeof(CollisionDetectionSystemGroup))]
    [UpdateAfter(typeof(CollisionDetectionSystem))]
    public partial struct SpaceshipCollisionSystem : ISystem
    {
        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<CollisionConfigComponent>();
            state.RequireForUpdate(
                SystemAPI.QueryBuilder()
                    .WithAll<SpaceshipCollisionElement, SpaceshipSpecComponent, LocalTransform>()
                    .Build()
            );
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            float restitution = SystemAPI.GetSingleton<CollisionConfigComponent>().Restitution;

            state.Dependency = new SpaceshipCollisionJob
            {
                Movements = SystemAPI.GetComponentLookup<SpaceshipMovementComponent>(true),
            }.ScheduleParallel(state.Dependency);

            // 속도 상태는 조종 주체마다 따로 들고 있어 나눠 반영함.
            JobHandle player = new PlayerCollisionResponseJob
            {
                Restitution = restitution,
            }.ScheduleParallel(state.Dependency);

            JobHandle enemy = new EnemyCollisionResponseJob
            {
                Restitution = restitution,
            }.ScheduleParallel(state.Dependency);

            state.Dependency = JobHandle.CombineDependencies(player, enemy);
        }
    }

    /// <summary>
    /// 데미지 식이 대칭이라 각 우주선이 자기 몫만 계산함.
    /// </summary>
    [BurstCompile]
    public partial struct SpaceshipCollisionJob : IJobEntity
    {
        [ReadOnly]
        public ComponentLookup<SpaceshipMovementComponent> Movements;

        private void Execute(
            in DynamicBuffer<SpaceshipCollisionElement> messages,
            ref LocalTransform transform,
            ref SpaceshipSpecComponent spec,
            in SpaceshipMovementComponent movement)
        {
            float2 direction = math.normalizesafe(movement.DeltaMovement, float2.zero);
            float speed = math.length(movement.DeltaMovement);
            float2 push = float2.zero;
            float damage = 0.0F;

            for (int i = 0; i < messages.Length; i++)
            {
                CollisionMessage message = messages[i].Message;
                float2 otherDirection = float2.zero;
                float otherSpeed = 0.0F;

                // 이번 틱에 파괴된 상대는 이동 정보가 없음. 밀어내기만 남김.
                if (Movements.TryGetComponent(message.Other, out SpaceshipMovementComponent other))
                {
                    otherDirection = math.normalizesafe(other.DeltaMovement, float2.zero);
                    otherSpeed = math.length(other.DeltaMovement);
                }

                // 서로 마주 볼수록 1에 가까워지는 정면 충돌 계수. 같은 방향으로 스치면 0.
                damage += speed * otherSpeed * math.max(0.0F, -math.dot(direction, otherDirection));

                // 깊이의 절반씩 서로 물러나면 딱 맞닿음.
                // 빠를수록 한 틱에 깊이 박히므로 밀려나는 양도 자동으로 커짐.
                push += message.Normal * (message.Depth * 0.5F);
            }

            // 판정에 쓴 위치는 밀어내기 전 위치라, 계산 순서에 관계없이 결과가 같음.
            transform.Position = new float3(transform.Position.xy + push, transform.Position.z);

            // 스치듯 지나가 데미지가 0인 충돌은 수리 대기를 걸지 않음.
            if (damage <= 0.0F)
            {
                return;
            }

            spec.Health = math.max(spec.Health - damage, 0.0F);
            spec.RepairCooldown = spec.RepairDelay;
        }
    }

    /// <summary>
    /// 플레이어 기체의 충돌 반응. 비행 속력은 기수 방향뿐이라
    /// 기수 성분만 속력에 넣고 나머지는 호버링 속도로 넘김.
    /// </summary>
    [BurstCompile]
    [WithAll(typeof(Simulate))]
    public partial struct PlayerCollisionResponseJob : IJobEntity
    {
        public float Restitution;

        private void Execute(
            in DynamicBuffer<SpaceshipCollisionElement> messages,
            in LocalTransform transform,
            in SpaceshipMovementComponent movement,
            ref PlayerSpaceshipMovementComponent player)
        {
            float2 impulse = CollisionResponse.Impulse(movement.DeltaMovement, messages, Restitution);

            if (math.lengthsq(impulse) <= math.EPSILON)
            {
                return;
            }

            float2 forward = math.normalizesafe(
                SpaceshipHeading.FromRotation(transform.Rotation),
                new float2(1.0F, 0.0F)
            );

            float along = math.dot(impulse, forward);

            player.Speed = math.clamp(player.Speed + along, 0.0F, player.MaxSpeed);
            player.HoveringVelocity += impulse - forward * along;
        }
    }

    /// <summary>
    /// AI 기체의 충돌 반응. 호버링 속도 하나뿐이라 그대로 더함.
    /// </summary>
    [BurstCompile]
    [WithAll(typeof(Simulate))]
    public partial struct EnemyCollisionResponseJob : IJobEntity
    {
        public float Restitution;

        private void Execute(
            in DynamicBuffer<SpaceshipCollisionElement> messages,
            in SpaceshipMovementComponent movement,
            ref EnemySpaceshipMovementComponent enemy)
        {
            enemy.Velocity += CollisionResponse.Impulse(movement.DeltaMovement, messages, Restitution);
        }
    }
}