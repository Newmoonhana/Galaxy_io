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
    /// AI 가 공격 대상으로 삼을 수 있는 우주선.
    /// </summary>
    public struct PerceptionTarget
    {
        public Entity Entity;
        public ColliderWorld Collider;
    }

    /// <summary>
    /// AI 의 공격 대상을 검증하고, 없으면 시야 안에서 새로 찾는 시스템.
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
                SystemAPI.QueryBuilder().WithAll<EnemyPerceptionComponent, LocalTransform>().Build()
            );
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            // 플레이어 수가 적어 매 틱 전수 비교함.
            // TODO: Spatial Hash, Faction 시스템 적용해 적 찾기
            var targets = new NativeList<PerceptionTarget>(Allocator.TempJob);

            state.Dependency = new GatherPerceptionTargetJob
            {
                Targets = targets,
            }.Schedule(state.Dependency);

            state.Dependency = new EnemyPerceptionJob
            {
                Targets = targets,
            }.ScheduleParallel(state.Dependency);

            state.Dependency = targets.Dispose(state.Dependency);
        }
    }

    /// <summary>
    /// 플레이어 우주선의 충돌체를 월드 좌표로 모음.
    /// </summary>
    [BurstCompile]
    [WithAll(typeof(PlayerSpaceshipMovementComponent))]
    public partial struct GatherPerceptionTargetJob : IJobEntity
    {
        public NativeList<PerceptionTarget> Targets;

        private void Execute(Entity entity, in LocalTransform transform, in ColliderComponent collider)
        {
            Targets.Add(new PerceptionTarget
            {
                Entity   = entity,
                Collider = ColliderOverlap.ToWorld(collider, transform),
            });
        }
    }

    /// <summary>
    /// 대상 유효성 검사 후, 대상이 없으면 시야에 겹친 가장 가까운 플레이어를 고름.
    /// 대상의 위치도 함께 기록함.
    /// </summary>
    [BurstCompile]
    [WithAll(typeof(Simulate))]
    public partial struct EnemyPerceptionJob : IJobEntity
    {
        [ReadOnly]
        public NativeList<PerceptionTarget> Targets;

        private void Execute(in LocalTransform transform, ref EnemyPerceptionComponent perception)
        {
            float2 position = transform.Position.xy;

            // 목록에 없으면 파괴됨. 추격 거리 밖이면 놓침.
            if (perception.CurrentTarget != Entity.Null)
            {
                for (int i = 0; i < Targets.Length; i++)
                {
                    if (Targets[i].Entity != perception.CurrentTarget)
                    {
                        continue;
                    }

                    float2 center = Targets[i].Collider.Center;

                    if (math.distancesq(position, center) <= perception.ChaseDistance * perception.ChaseDistance)
                    {
                        perception.TargetPosition = center;
                        return;
                    }

                    break;
                }

                perception.CurrentTarget = Entity.Null;
            }

            // 시야도 우주선 크기를 따라 커짐.
            ColliderWorld sight = ColliderOverlap.ToWorld(perception.Sight, transform);
            float nearestSq = float.MaxValue;

            for (int i = 0; i < Targets.Length; i++)
            {
                if (!ColliderOverlap.Test(sight, Targets[i].Collider, out _))
                {
                    continue;
                }

                float distanceSq = math.distancesq(position, Targets[i].Collider.Center);

                if (distanceSq < nearestSq)
                {
                    nearestSq = distanceSq;
                    perception.CurrentTarget = Targets[i].Entity;
                    perception.TargetPosition = Targets[i].Collider.Center;
                }
            }
        }
    }
}