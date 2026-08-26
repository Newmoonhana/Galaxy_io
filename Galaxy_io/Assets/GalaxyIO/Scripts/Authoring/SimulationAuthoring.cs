using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

namespace GalaxyIO
{
    /// <summary>
    /// The one authoring component for the whole simulation. Drop it on a GameObject inside
    /// a SubScene; the baker below turns it into the singleton entity every system reads.
    ///
    /// Referencing the two GameObject prefabs here is what makes them bake into *entity*
    /// prefabs: <c>GetEntity(prefab, ...)</c> pulls the prefab into the entity scene along
    /// with its MeshFilter/MeshRenderer, which Entities Graphics converts into the
    /// MaterialMeshInfo + RenderBounds needed to draw them.
    /// </summary>
    [AddComponentMenu("ECS Simulator/Simulation Authoring")]
    [DisallowMultipleComponent]
    public class SimulationAuthoring : MonoBehaviour
    {
        [Header("Entity prefabs")]
        public GameObject BotPrefab;
        public GameObject PlayerPrefab;

        [Header("World bounds")]
        public Vector2 BoundsMin = new Vector2(-1000f, -1000f);
        public Vector2 BoundsMax = new Vector2(1000f, 1000f);

        [Header("Population")]
        [Min(0)] public int InitialMaxBotCount = 1000;
        [Tooltip("Upper limit accepted by the HUD input field.")]
        [Min(1)] public int HardMaxBotCount = 200000;
        [Tooltip("How many bots may be instantiated in a single frame.")]
        [Min(1)] public int MaxSpawnsPerFrame = 1000;

        [Header("Bots")]
        [Min(0.01f)] public float BotSize = 12f;
        [Min(0f)] public float BotSpeedMin = 40f;
        [Min(0f)] public float BotSpeedMax = 200f;

        [Header("Player")]
        [Min(0.01f)] public float PlayerSize = 40f;
        [Min(0f)] public float PlayerSpeed = 600f;

        [Header("Determinism")]
        public uint RandomSeed = 24601u;

        void OnValidate()
        {
            BotSpeedMax = Mathf.Max(BotSpeedMax, BotSpeedMin);
            BoundsMax = Vector2.Max(BoundsMax, BoundsMin + Vector2.one);
            HardMaxBotCount = Mathf.Max(HardMaxBotCount, InitialMaxBotCount);
            if (RandomSeed == 0u)
                RandomSeed = 1u;
        }

        void OnDrawGizmosSelected()
        {
            Gizmos.color = Color.yellow;
            var center = (Vector3)(Vector2)((BoundsMin + BoundsMax) * 0.5f);
            var size = (Vector3)(Vector2)(BoundsMax - BoundsMin);
            Gizmos.DrawWireCube(center, size);
        }
    }

    public class SimulationBaker : Baker<SimulationAuthoring>
    {
        public override void Bake(SimulationAuthoring authoring)
        {
            // TransformUsageFlags.None: this entity is pure configuration, it is never drawn
            // and never moves, so it does not need any transform components.
            var entity = GetEntity(TransformUsageFlags.None);

            AddComponent(entity, new SimulationConfig
            {
                BotPrefab = GetEntity(authoring.BotPrefab, TransformUsageFlags.Dynamic),
                PlayerPrefab = GetEntity(authoring.PlayerPrefab, TransformUsageFlags.Dynamic),
                MaxBotCount = authoring.InitialMaxBotCount,
                MaxSpawnsPerFrame = authoring.MaxSpawnsPerFrame,
                BotSize = authoring.BotSize,
                BotSpeedMin = authoring.BotSpeedMin,
                BotSpeedMax = math.max(authoring.BotSpeedMax, authoring.BotSpeedMin),
                PlayerSize = authoring.PlayerSize,
                PlayerSpeed = authoring.PlayerSpeed
            });

            AddComponent(entity, new SimulationLimits
            {
                HardMaxBotCount = authoring.HardMaxBotCount
            });

            AddComponent(entity, new WorldBounds
            {
                Min = new float2(authoring.BoundsMin.x, authoring.BoundsMin.y),
                Max = new float2(authoring.BoundsMax.x, authoring.BoundsMax.y)
            });

            AddComponent(entity, new SimulationRandom
            {
                Value = Unity.Mathematics.Random.CreateFromIndex(authoring.RandomSeed)
            });

            AddComponent<SimulationStats>(entity);
            AddComponent<PlayerInputState>(entity);
        }
    }
}