using Unity.Entities;
using Unity.Mathematics;

namespace GalaxyIO
{
    /// <summary>Tag identifying the single player-controlled box.</summary>
    public struct PlayerTag : IComponentData { }

    /// <summary>Tag identifying an AI ("bot") box.</summary>
    public struct BotTag : IComponentData { }

    /// <summary>Linear velocity in world units per second. XY only; the sim is 2D.</summary>
    public struct Velocity : IComponentData
    {
        public float2 Value;
    }

    /// <summary>
    /// Axis-aligned square collider. <see cref="Value"/> is the full edge length in world
    /// units, and it doubles as the uniform <c>LocalTransform.Scale</c> because the render
    /// mesh is a 1x1 quad.
    /// </summary>
    public struct BoxSize : IComponentData
    {
        public float Value;
    }

    /// <summary>Singleton. The rectangle every unit is confined to.</summary>
    public struct WorldBounds : IComponentData
    {
        public float2 Min;
        public float2 Max;
    }

    /// <summary>Singleton. Authored in the sub scene, tweakable at runtime from the HUD.</summary>
    public struct SimulationConfig : IComponentData
    {
        /// <summary>Baked entity prefab for bots. Instantiated by <c>BotSpawnSystem</c>.</summary>
        public Entity BotPrefab;

        /// <summary>Baked entity prefab for the player. Instantiated by <c>PlayerSpawnSystem</c>.</summary>
        public Entity PlayerPrefab;

        /// <summary>Population cap. Driven by the HUD input field.</summary>
        public int MaxBotCount;

        /// <summary>Spawn budget per frame, so raising the cap by a lot does not hitch.</summary>
        public int MaxSpawnsPerFrame;

        public float BotSize;
        public float BotSpeedMin;
        public float BotSpeedMax;

        public float PlayerSize;
        public float PlayerSpeed;
    }

    /// <summary>Singleton. Authoring-time guard rails the HUD clamps user input against.</summary>
    public struct SimulationLimits : IComponentData
    {
        public int HardMaxBotCount;
    }

    /// <summary>Singleton. Read-only mirror of the sim for the HUD.</summary>
    public struct SimulationStats : IComponentData
    {
        public int AliveBotCount;
        public int DestroyedBotCount;
        public int DestroyedThisFrame;
    }

    /// <summary>Singleton. Deterministic RNG shared by the spawn systems.</summary>
    public struct SimulationRandom : IComponentData
    {
        public Random Value;
    }

    /// <summary>
    /// Singleton written once per frame by <c>PlayerInputSystem</c> and read by
    /// <c>PlayerMovementSystem</c>. This is the standard way to get managed engine input
    /// into Burst-compiled jobs: sample it on the main thread, publish it as plain data.
    /// </summary>
    public struct PlayerInputState : IComponentData
    {
        /// <summary>Normalized-ish move axis, -1..1 per component.</summary>
        public float2 Move;
    }
}