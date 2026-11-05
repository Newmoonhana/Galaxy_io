using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.InputSystem;

namespace GalaxyIO
{
    /// <summary>
    /// 시뮬레이션 정보를 담고 있는 오브젝트. DOTS 객체들은 Managed 메모리 영역에 있는 오브젝트들과
    /// 분리되도록 설계되었기 때문에, 시뮬레이션의 사전 설정들도 DOTS의 Unmanaged 영역으로 끌고 온다.
    /// </summary>
    [AddComponentMenu("ECS Simulator/Simulation Authoring")]
    [DisallowMultipleComponent]
    public class SimulationAuthoring : MonoBehaviour
    {
        [Header("Entity prefabs")]

        public GameObject BotPrefab;
        public GameObject PlayerPrefab;

        [Header("Input Actions")]

        public InputActionAsset InputActionAsset;

        [Header("World bounds")]

        public Vector2 BoundsMin = new (-1000F, -1000F);
        public Vector2 BoundsMax = new (1000F, 1000F);

        [Header("Population")]

        [Min(0)]
        public int InitialMaxBotCount = 1000;

        [Tooltip("Upper limit accepted by the HUD input field.")]
        [Min(1)]
        public int HardMaxBotCount = 200000;

        [Tooltip("How many bots may be instantiated in a single frame.")]
        [Min(1)]
        public int MaxSpawnsPerFrame = 1000;

        [Header("Bots")]

        [Min(0.01f)]
        public float BotSize = 12F;

        [Min(0f)]
        public float BotSpeedMin = 40F;

        [Min(0f)]
        public float BotSpeedMax = 200F;

        [Header("Player")]

        [Min(0.01f)]
        public float PlayerSize = 40F;

        [Min(0f)]
        public float PlayerSpeed = 600F;

        [Header("Determinism")]

        public uint RandomSeed = 24601u;

        /// <summary>
        /// 데이터 검증
        /// </summary>
        private void OnValidate()
        {
            BotSpeedMax = Mathf.Max(BotSpeedMax, BotSpeedMin);
            BoundsMax = Vector2.Max(BoundsMax, BoundsMin + Vector2.one);
            HardMaxBotCount = Mathf.Max(HardMaxBotCount, InitialMaxBotCount);

            if (RandomSeed == 0u)
            {
                RandomSeed = 1u;
            }
        }
    }

    public class SimulationBaker : Baker<SimulationAuthoring>
    {
        public override void Bake(SimulationAuthoring authoring)
        {
            // TransformUsageFlags.None: 이 오브젝트는 순수하게 설정만을 위한 것이며 움직이지 않고,
            // 렌더링 되지 않습니다. 따라서 Transform Component가 필요하지 않습니다.
            var entity = GetEntity(TransformUsageFlags.None);

            AddComponent(entity, new SimulationConfig
            {
                BotPrefab         = GetEntity(authoring.BotPrefab, TransformUsageFlags.Dynamic), // Prefab => Entity 로 변환
                PlayerPrefab      = GetEntity(authoring.PlayerPrefab, TransformUsageFlags.Dynamic),
                MaxBotCount       = authoring.InitialMaxBotCount,
                MaxSpawnsPerFrame = authoring.MaxSpawnsPerFrame,
                BotSize           = authoring.BotSize,
                BotSpeedMin       = authoring.BotSpeedMin,
                BotSpeedMax       = math.max(authoring.BotSpeedMax, authoring.BotSpeedMin),
                PlayerSize        = authoring.PlayerSize,
                PlayerSpeed       = authoring.PlayerSpeed
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

            // Managed 오브젝트는 AddComponentObject로만 붙일 수 있습니다. DependsOn을 걸어 두면
            // 에셋이 교체됐을 때 SubScene이 자동으로 다시 베이크됩니다.
            if (authoring.InputActionAsset != null)
            {
                DependsOn(authoring.InputActionAsset);
            }

            AddComponentObject(entity, new PlayerInputAsset
            {
                Value = authoring.InputActionAsset
            });

            AddComponent<SimulationStats>(entity);
            AddComponent<PlayerInputState>(entity);
        }
    }
}