using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Entities;

namespace GalaxyIO
{
    // ---------------------------------------------------------------------------------
    //  FRAME LAYOUT
    //
    //  InitializationSystemGroup
    //    PlayerInputSystem ............ samples the Input System, publishes PlayerInputState
    //
    //  SimulationSystemGroup
    //    PlayerSpawnSystem ............ makes sure exactly one player exists
    //    BotSpawnSystem ............... tops the population up to SimulationConfig.MaxBotCount
    //    MovementSystemGroup .......... integrates velocity, bounces off the world bounds
    //    CollisionSystemGroup ......... PRODUCER: emits BotHitPlayerEvent entities
    //    EventCommandBufferSystem ..... plays the producer's command buffer back
    //    EventConsumerSystemGroup ..... CONSUMERS: BotDespawnSystem, ScoreSystem
    //    EventCleanupSystem ........... destroys every event entity (one-frame lifetime)
    //    TransformSystemGroup ......... (built in) LocalTransform -> LocalToWorld
    //
    //  PresentationSystemGroup
    //    (Entities Graphics renders LocalToWorld + MaterialMeshInfo)
    // ---------------------------------------------------------------------------------

    /// <summary>Velocity integration and world-bounds response.</summary>
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    public partial class MovementSystemGroup : ComponentSystemGroup { }

    /// <summary>Overlap detection. Everything in here only *produces* messages.</summary>
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateAfter(typeof(MovementSystemGroup))]
    public partial class CollisionSystemGroup : ComponentSystemGroup { }

    /// <summary>
    /// Everything in here only *reacts* to messages. Consumers never call each other and
    /// never call back into the producer.
    /// </summary>
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateAfter(typeof(EventCommandBufferSystem))]
    public partial class EventConsumerSystemGroup : ComponentSystemGroup { }

    /// <summary>
    /// A dedicated <see cref="EntityCommandBufferSystem"/> that sits between the producers
    /// and the consumers. Producers ask it for a command buffer; it plays every pending
    /// buffer back during its own update, which is the moment the event entities actually
    /// come into existence.
    ///
    /// The nested <see cref="Singleton"/> struct is the boilerplate Unity requires so that
    /// Burst-compiled <c>ISystem</c>s can reach the buffer through <c>SystemAPI.GetSingleton</c>
    /// without touching managed objects. Accessing the singleton also registers the job
    /// dependency, so the ECB system automatically waits for the producer jobs.
    /// </summary>
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateAfter(typeof(CollisionSystemGroup))]
    public partial class EventCommandBufferSystem : EntityCommandBufferSystem
    {
        public unsafe struct Singleton : IComponentData, IECBSingleton
        {
            internal UnsafeList<EntityCommandBuffer>* pendingBuffers;
            internal AllocatorManager.AllocatorHandle allocator;

            public EntityCommandBuffer CreateCommandBuffer(WorldUnmanaged world)
            {
                return EntityCommandBufferSystem.CreateCommandBuffer(ref *pendingBuffers, allocator, world);
            }

            public void SetPendingBufferList(ref UnsafeList<EntityCommandBuffer> buffers)
            {
                pendingBuffers = (UnsafeList<EntityCommandBuffer>*)UnsafeUtility.AddressOf(ref buffers);
            }

            public void SetAllocator(Allocator allocatorIn) => allocator = allocatorIn;

            public void SetAllocator(AllocatorManager.AllocatorHandle allocatorIn) => allocator = allocatorIn;
        }

        protected override void OnCreate()
        {
            base.OnCreate();
            this.RegisterSingleton<Singleton>(ref PendingBuffers, World.Unmanaged);
        }
    }
}