using Unity.Burst;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace GalaxyIO
{
    /// <summary>
    /// PRODUCER.
    ///
    /// Tests every bot against the player AABB and, for each overlap, emits one
    /// <see cref="BotHitPlayerEvent"/> entity. It deliberately does NOT destroy anything
    /// and does NOT touch the score: it only reports what happened. Whoever wants to react
    /// subscribes by querying for the message, which means new reactions (VFX, audio,
    /// analytics, achievements) can be added later without editing this file.
    ///
    /// Creating an entity is a structural change and therefore illegal inside a job, so the
    /// job records the creation into an <see cref="EntityCommandBuffer.ParallelWriter"/>
    /// handed out by <see cref="EventCommandBufferSystem"/>. That system updates right after
    /// this group and plays the buffer back, at which point the event entities exist for
    /// the consumers to find.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(CollisionSystemGroup))]
    public partial struct CollisionDetectionSystem : ISystem
    {
        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<EventCommandBufferSystem.Singleton>();
            state.RequireForUpdate(SystemAPI.QueryBuilder().WithAll<PlayerTag>().Build());
            state.RequireForUpdate(SystemAPI.QueryBuilder().WithAll<BotTag>().Build());
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var player = SystemAPI.GetSingletonEntity<PlayerTag>();
            var playerTransform = SystemAPI.GetComponent<LocalTransform>(player);
            var playerSize = SystemAPI.GetComponent<BoxSize>(player);

            // Asking for the ECB singleton also registers the dependency, so the command
            // buffer system knows to wait for this job before playing anything back.
            var commandBuffer = SystemAPI
                .GetSingleton<EventCommandBufferSystem.Singleton>()
                .CreateCommandBuffer(state.WorldUnmanaged)
                .AsParallelWriter();

            state.Dependency = new DetectPlayerOverlapJob
            {
                Player = player,
                PlayerCenter = playerTransform.Position.xy,
                PlayerHalfExtent = playerSize.Value * 0.5f,
                CommandBuffer = commandBuffer
            }.ScheduleParallel(state.Dependency);
        }
    }

    [BurstCompile]
    [WithAll(typeof(BotTag))]
    public partial struct DetectPlayerOverlapJob : IJobEntity
    {
        public Entity Player;
        public float2 PlayerCenter;
        public float PlayerHalfExtent;
        public EntityCommandBuffer.ParallelWriter CommandBuffer;

        void Execute([ChunkIndexInQuery] int chunkIndex, Entity bot, in LocalTransform transform, in BoxSize size)
        {
            var delta = math.abs(transform.Position.xy - PlayerCenter);
            var combinedHalfExtent = PlayerHalfExtent + size.Value * 0.5f;

            if (delta.x > combinedHalfExtent || delta.y > combinedHalfExtent)
            {
                return;
            }

            // ---- publish the message ----
            var message = CommandBuffer.CreateEntity(chunkIndex);
            CommandBuffer.AddComponent(chunkIndex, message, new BotHitPlayerEvent
            {
                Bot = bot,
                Player = Player,
                Position = transform.Position.xy
            });

            CommandBuffer.AddComponent<SimulationEvent>(chunkIndex, message);
        }
    }
}