using GalaxyIO.DOTS.Collision;
using GalaxyIO.DOTS.Components.Actor;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace GalaxyIO.DOTS.Systems
{
    /// <summary>
    /// AI 의 공격 대상을 검증하고, 없으면 시야 메시지 중에서 새로 고르는 시스템.
    /// </summary>
    [BurstCompile]
    [WorldSystemFilter(WorldSystemFilterFlags.ServerSimulation)]
    [UpdateInGroup(typeof(AISystemGroup))]
    public partial struct EnemyPerceptionSystem : ISystem
    {
        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate(
                SystemAPI.QueryBuilder()
                    .WithAll<EnemyPerceptionComponent, SightOverlapElement, LocalTransform>()
                    .Build()
            );
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            state.Dependency = new EnemyPerceptionJob
            {
                Transforms = SystemAPI.GetComponentLookup<LocalTransform>(true),
                Colliders  = SystemAPI.GetComponentLookup<ColliderComponent>(true),
            }.ScheduleParallel(state.Dependency);
        }
    }

    /// <summary>
    /// 대상 유효성 검사 후, 대상이 없으면 시야에 겹친 가장 가까운 우주선을 고름.
    /// 진영 필터는 판정에서 이미 적용됨.
    /// </summary>
    [BurstCompile]
    [WithAll(typeof(Simulate))]
    public partial struct EnemyPerceptionJob : IJobEntity
    {
        [ReadOnly]
        public ComponentLookup<LocalTransform> Transforms;

        [ReadOnly]
        public ComponentLookup<ColliderComponent> Colliders;

        private void Execute(
            in DynamicBuffer<SightOverlapElement> overlaps,
            in LocalTransform transform,
            ref EnemyPerceptionComponent perception)
        {
            float2 position = transform.Position.xy;

            // 사라졌으면 놓침. 추격 거리 밖이면 놓침. 피격으로 정한 대상도 있으므로 진영은 보지 않음.
            if (perception.CurrentTarget != Entity.Null)
            {
                if (TryGetCenter(perception.CurrentTarget, out float2 center)
                    && math.distancesq(position, center) <= perception.ChaseDistance * perception.ChaseDistance)
                {
                    perception.TargetPosition = center;

                    return;
                }

                perception.CurrentTarget = Entity.Null;
            }

            // 메시지는 가까운 순으로 쌓임.
            for (int i = 0; i < overlaps.Length; i++)
            {
                CollisionMessage message = overlaps[i].Message;

                if (!TryGetCenter(message.Other, out float2 center))
                {
                    continue;
                }

                perception.CurrentTarget = message.Other;
                perception.TargetPosition = center;

                return;
            }
        }

        // 대상 충돌체의 월드 중심. 이미 파괴됐으면 false.
        private bool TryGetCenter(Entity entity, out float2 center)
        {
            if (Colliders.TryGetComponent(entity, out ColliderComponent collider)
                && Transforms.TryGetComponent(entity, out LocalTransform transform))
            {
                center = ColliderOverlap.ToWorld(collider, transform).Center;

                return true;
            }

            center = float2.zero;

            return false;
        }
    }
}
