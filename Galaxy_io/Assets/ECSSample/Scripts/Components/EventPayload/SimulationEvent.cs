using Unity.Entities;

namespace GalaxyIO
{
    // ---------------------------------------------------------------------------------
    //  MESSAGING / EVENTS IN UNITY DOTS
    // ---------------------------------------------------------------------------------
    //  Unity's ECS has no built-in "event bus". The idiomatic pattern - the one used by
    //  the Entities samples and by shipped DOTS titles - is the *event entity*:
    //
    //    1. A producer system detects something and creates a brand new entity that holds
    //       nothing but the message payload (an IComponentData) plus a marker tag.
    //       Because creating entities is a structural change, producers running in jobs
    //       write into an EntityCommandBuffer instead of touching the EntityManager.
    //
    //    2. An EntityCommandBufferSystem placed between the producer and the consumers
    //       plays the buffer back, so the event entities exist for the rest of the frame.
    //
    //    3. Any number of consumer systems query for the message component. They do not
    //       know about each other and they do not know about the producer - that decoupling
    //       is the entire point. Here BotDespawnSystem and ScoreSystem both react to the
    //       same BotHitPlayerEvent.
    //
    //    4. A cleanup system at the end of the frame destroys every event entity, giving
    //       messages a well-defined one-frame lifetime. Nothing accumulates, and there is
    //       no subscribe/unsubscribe bookkeeping to leak.
    //
    //  Alternatives you will also see in the wild, and when they are preferable:
    //    * DynamicBuffer<T> (IBufferElementData) used as a per-entity inbox - better when
    //      the message is addressed to one specific entity (damage, orders) because the
    //      consumer can look it up instead of scanning all events.
    //    * NativeQueue/NativeStream .ParallelWriter drained on the main thread - avoids
    //      the structural change entirely; use when the volume is huge and consumers are
    //      known up front.
    //  The event-entity pattern is the general-purpose default, so that is what this
    //  project implements. Every message type in this folder carries the SimulationEvent
    //  tag below alongside its payload component.
    // ---------------------------------------------------------------------------------

    /// <summary>
    /// Marker present on every event entity in this project, regardless of payload.
    /// <c>EventCleanupSystem</c> uses it to wipe all messages with a single query.
    /// </summary>
    public struct SimulationEvent : IComponentData { }
}
