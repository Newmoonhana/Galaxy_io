using UnityEngine;
using UnityEngine.InputSystem;

namespace OOPSample
{
    // ---------------------------------------------------------------------------------
    //  FRAME LAYOUT
    //
    //  ECS 샘플의 시스템 그룹 배치를 그대로 옮긴 것입니다. DOTS에서는 SystemBase/ISystem을
    //  그룹에 넣고 UpdateBefore/UpdateAfter로 순서를 선언하면 되지만, MonoBehaviour에는
    //  그런 선언적 스케줄러가 없으므로 (a) Runner가 명시적으로 호출하는 단계와
    //  (b) DefaultExecutionOrder로 줄 세운 개체별 Update를 섞어 같은 순서를 만듭니다.
    //
    //  SimulationRunner.Update            [DefaultExecutionOrder(-1000)]
    //    PlayerInputReader.Sample() ....... 입력 샘플링           (ECS: PlayerInputSystem)
    //    PlayerSpawner.Tick() ............. 플레이어 1개 보장      (ECS: PlayerSpawnSystem)
    //    BotSpawner.Tick() ................ 개체수를 상한에 맞춤   (ECS: BotSpawnSystem)
    //
    //  PlayerBehaviour.Update             [DefaultExecutionOrder(-500)]
    //    입력 -> 속도 -> 위치, 경계 clamp                        (ECS: PlayerMovementSystem)
    //
    //  BotBehaviour.Update                [DefaultExecutionOrder(0)]
    //    적분 + 벽 반사                                          (ECS: BotMovementSystem)
    //    플레이어와 AABB 검사 -> 메시지 발행  PRODUCER            (ECS: CollisionDetectionSystem)
    //
    //  SimulationRunner.LateUpdate        [DefaultExecutionOrder(-1000)]
    //    bus.DispatchAll() ................ CONSUMERS            (ECS: EventCommandBufferSystem
    //      BotDespawner / ScoreKeeper                                  + EventConsumerSystemGroup)
    //    bus.ClearAll() ................... 한 프레임 수명        (ECS: EventCleanupSystem)
    //
    //  SimulationHud.Update / LateUpdate  [DefaultExecutionOrder(500)]
    //
    //  렌더링은 MeshRenderer + GPU 인스턴싱이 담당합니다. ECS 쪽의 Entities Graphics(BRG)에
    //  대응하는 자리이며, 두 구현의 성능 차이 중 상당 부분이 실제로 여기서 갈립니다.
    // ---------------------------------------------------------------------------------

    /// <summary>
    /// 시뮬레이션의 합성 루트(composition root)이자 프레임 스케줄러.
    ///
    /// ECS 샘플에서 <c>SimulationAuthoring</c>(설정 베이킹) + <c>SimulationSystemGroups</c>
    /// (프레임 배치)가 나눠 맡던 역할을 하나로 합친 것입니다. 베이킹 단계가 없으므로 인스펙터
    /// 값이 곧 런타임 값이고, SubScene도 필요 없습니다.
    /// </summary>
    [AddComponentMenu("OOP Simulator/Simulation Runner")]
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(-1000)]
    public sealed class SimulationRunner : MonoBehaviour
    {
        [Header("Simulation")]

        [SerializeField]
        private SimulationConfig m_Config = new SimulationConfig();

        [SerializeField]
        private SimulationLimits m_Limits = new SimulationLimits();

        [Header("Input Actions")]

        [SerializeField]
        private InputActionAsset m_InputActionAsset;

        [Header("World bounds")]

        [SerializeField]
        private Vector2 m_BoundsMin = new Vector2(-1000f, -1000f);

        [SerializeField]
        private Vector2 m_BoundsMax = new Vector2(1000f, 1000f);

        [Header("Determinism")]

        [SerializeField]
        private uint m_RandomSeed = 24601u;

        private readonly SimulationStats m_Stats = new SimulationStats();
        private readonly BotRegistry m_Registry = new BotRegistry();
        private readonly SimulationEventBus m_Bus = new SimulationEventBus();

        private PlayerInputReader m_Input;
        private PlayerSpawner m_PlayerSpawner;
        private BotSpawner m_BotSpawner;
        private BotDespawner m_BotDespawner;
        private ScoreKeeper m_ScoreKeeper;

        /// <summary>HUD가 읽어 가는 카운터. ECS 쪽 <c>SimulationStats</c> 싱글톤 자리입니다.</summary>
        public SimulationStats Stats => m_Stats;

        /// <summary>HUD가 clamp 기준으로 삼는 상한.</summary>
        public int HardMaxBotCount => m_Limits.HardMaxBotCount;

        /// <summary>
        /// HUD가 읽고 쓰는 개체수 상한. ECS 쪽에서는 EntityQuery로 싱글톤을 잡아 쓰기까지
        /// 해야 했지만, 여기서는 그냥 프로퍼티입니다. <c>BotSpawner</c>가 다음 Tick에 반영합니다.
        /// </summary>
        public int MaxBotCount
        {
            get => m_Config.MaxBotCount;
            set => m_Config.MaxBotCount = Mathf.Clamp(value, 0, m_Limits.HardMaxBotCount);
        }

        private void Awake()
        {
            var bounds = new WorldBounds(m_BoundsMin, m_BoundsMax);

            m_Input = new PlayerInputReader(m_InputActionAsset);

            // 스폰된 박스를 전부 이 오브젝트 밑에 넣습니다. 하이어라키 루트가 10만 개로 부풀면
            // 에디터가 감당하지 못합니다(플레이 모드 하이어라키 창을 닫아 두는 편이 좋습니다).
            m_PlayerSpawner = new PlayerSpawner(m_Config, m_Input, bounds, transform);

            m_BotSpawner = new BotSpawner(
                m_Config, m_Stats, m_Bus, m_Registry, m_PlayerSpawner, bounds, transform, m_RandomSeed);

            // 컨슈머는 생성자에서 버스를 구독합니다. 프로듀서(BotBehaviour)는 이들의 존재를
            // 전혀 모르고, 컨슈머끼리도 서로를 모릅니다.
            m_BotDespawner = new BotDespawner(m_Bus, m_Registry);
            m_ScoreKeeper = new ScoreKeeper(m_Bus, m_Stats);
        }

        private void OnEnable() => m_Input.Enable();

        private void OnDisable()
        {
            m_Input.Disable();
            PlayerInputReader.UiHasFocus = false;
        }

        private void OnDestroy()
        {
            // ECS의 이벤트 엔티티에는 없던 뒷정리. 구독을 남겨 두면 플레이 모드를 반복할 때
            // 죽은 컨슈머가 계속 호출됩니다.
            m_BotDespawner?.Dispose();
            m_ScoreKeeper?.Dispose();
            m_Bus.ClearAll();
        }

        private void Update()
        {
            SimulationTime.DeltaTime = Time.deltaTime;

            m_Input.Sample();
            m_PlayerSpawner.Tick();
            m_BotSpawner.Tick();
        }

        private void LateUpdate()
        {
            // 이 시점에는 모든 봇이 이번 프레임 이동과 충돌 검사를 마쳤습니다.
            m_Bus.DispatchAll();
            m_Bus.ClearAll();
        }

        /// <summary>ECS 샘플의 <c>SimulationAuthoring.OnValidate</c>와 같은 데이터 검증입니다.</summary>
        private void OnValidate()
        {
            if (m_Config == null || m_Limits == null)
                return;

            m_Config.BotSpeedMax = Mathf.Max(m_Config.BotSpeedMax, m_Config.BotSpeedMin);
            m_BoundsMax = Vector2.Max(m_BoundsMax, m_BoundsMin + Vector2.one);
            m_Limits.HardMaxBotCount = Mathf.Max(m_Limits.HardMaxBotCount, m_Config.MaxBotCount);

            if (m_RandomSeed == 0u)
            {
                m_RandomSeed = 1u;
            }
        }

#if UNITY_EDITOR
        /// <summary>씬 빌더가 인스펙터 값을 채워 넣을 때만 씁니다.</summary>
        public void ConfigureForBuild(
            BotBehaviour botPrefab,
            PlayerBehaviour playerPrefab,
            InputActionAsset inputActionAsset,
            Vector2 boundsMin,
            Vector2 boundsMax)
        {
            m_Config.BotPrefab = botPrefab;
            m_Config.PlayerPrefab = playerPrefab;
            m_InputActionAsset = inputActionAsset;
            m_BoundsMin = boundsMin;
            m_BoundsMax = boundsMax;
        }
#endif
    }
}
