using GalaxyIO.DOTS.Collision;
using GalaxyIO.DOTS.Components.Actor;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.NetCode;
using Unity.Transforms;

namespace GalaxyIO.DOTS.Systems
{
    /// <summary>
    /// 명중한 것으로 보이는 투사체를 화면에서 감추는 클라이언트 전용 시스템.
    /// 서버 판정이 도착하기 전까지 탄이 표적을 지나쳐 날아가는 것을 막음.
    /// </summary>
    [BurstCompile]
    [WorldSystemFilter(WorldSystemFilterFlags.ClientSimulation)]
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    public partial struct ProjectileHitPresentationSystem : ISystem
    {
        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<EndSimulationEntityCommandBufferSystem.Singleton>();
            state.RequireForUpdate(SystemAPI.QueryBuilder().WithAll<SharedProjectileComponent>().Build());
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var spaceships = new NativeList<SpaceshipCollider>(Allocator.TempJob);

            // Spaceship Collider 수집
            state.Dependency = new GatherSpaceshipColliderJob
            {
                Spaceships = spaceships,
            }.Schedule(state.Dependency);

            state.Dependency = new ProjectileHidingJob
            {
                Spaceships = spaceships,
                CommandBuffer = SystemAPI
                    .GetSingleton<EndSimulationEntityCommandBufferSystem.Singleton>()
                    .CreateCommandBuffer(state.WorldUnmanaged).AsParallelWriter(),
            }.ScheduleParallel(state.Dependency);

            state.Dependency = spaceships.Dispose(state.Dependency);
        }
    }

    /// <summary>
    /// 명중한 투사체를 화면에서 지움.
    /// <see cref="Disabled"/>가 붙으면 Entities가 스프라이트를 들고 있는 GameObject를 꺼 줌.
    /// 한 번 지운 투사체는 서버가 despawn 할 때까지 다시 보이지 않음.
    /// </summary>
    [BurstCompile]
    public partial struct ProjectileHidingJob : IJobEntity
    {
        [ReadOnly]
        public NativeList<SpaceshipCollider> Spaceships;

        public EntityCommandBuffer.ParallelWriter CommandBuffer;

        private void Execute(
            Entity entity,
            [ChunkIndexInQuery] int chunkIndex,
            in LocalTransform transform,
            ref SharedProjectileComponent shared,
            in ColliderComponent collider,
            in GhostOwner owner)
        {
            if (shared.Hidden)
            {
                return;
            }

            float2 position = transform.Position.xy;
            float2 travelled = position - shared.LastTestedPosition;
            bool first = !shared.Tested;

            shared.LastTestedPosition = position;
            shared.Tested = true;

            // 갓 스폰한 투사체는 비교할 직전 위치가 없음.
            if (first)
            {
                return;
            }

            // 프레임 사이에 지나간 구간 전체를 훑음. 표적을 건너뛰지 않음.
            ColliderWorld world = ColliderOverlap.Sweep(
                ColliderOverlap.ToWorld(collider, transform),
                travelled
            );

            for (int i = 0; i < Spaceships.Length; i++)
            {
                // 자기가 쏜 탄은 통과. 총구가 선체에 가까워도 발사 즉시 사라지지 않음.
                if (Spaceships[i].OwnerNetworkId == owner.NetworkId)
                {
                    continue;
                }

                if (!ColliderOverlap.Test(world, Spaceships[i].Collider, out _))
                {
                    continue;
                }

                shared.Hidden = true;
                CommandBuffer.AddComponent<Disabled>(chunkIndex, entity);

                return;
            }
        }
    }
}