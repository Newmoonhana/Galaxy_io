using System.Globalization;
using Unity.Entities;
using UnityEngine;
using UnityEngine.UI;

namespace GalaxyIO
{
    /// <summary>
    /// The GameObject-side control panel.
    ///
    /// GameObjects and entities live in different worlds, so a MonoBehaviour cannot "call"
    /// a system. The supported way across the boundary is exactly what this class does:
    /// grab <see cref="World.DefaultGameObjectInjectionWorld"/>, run a query for the
    /// singleton, and read or write plain component data. The systems pick the new value up
    /// on their next update - no callbacks, no coupling in the other direction.
    /// </summary>
    [AddComponentMenu("ECS Simulator/Simulation HUD")]
    public class SimulationHud : MonoBehaviour
    {
        [SerializeField]
        private InputField m_MaxBotsField;

        [SerializeField]
        private Button m_ApplyButton;

        [SerializeField]
        private Text m_StatsText;

        private EntityQuery m_ConfigQuery;
        private EntityQuery m_LimitsQuery;
        private EntityQuery m_StatsQuery;

        private bool m_QueriesReady;

        private float m_SmoothedDeltaTime;
        private int m_LastAppliedMax = -1;

        private void OnEnable()
        {
            if (m_ApplyButton != null)
                m_ApplyButton.onClick.AddListener(ApplyFromField);

            if (m_MaxBotsField != null)
            {
                m_MaxBotsField.contentType = InputField.ContentType.IntegerNumber;
                m_MaxBotsField.onEndEdit.AddListener(_ => ApplyFromField());
            }
        }

        private void OnDisable()
        {
            if (m_ApplyButton != null)
                m_ApplyButton.onClick.RemoveListener(ApplyFromField);

            if (m_MaxBotsField != null)
                m_MaxBotsField.onEndEdit.RemoveAllListeners();

            PlayerInputSystem.UiHasFocus = false;

            ReleaseQueries();
        }

        private void Update()
        {
            // While the field has keyboard focus, WASD belongs to the text box, not the player.
            PlayerInputSystem.UiHasFocus = m_MaxBotsField != null && m_MaxBotsField.isFocused;

            if (!EnsureQueries())
            {
                return;
            }

            SyncFieldWithSimulation();
            RefreshStats();
        }

        /// <summary>
        /// Entity Queries가 열려있는지 보장하는 함수.
        /// </summary>
        private bool EnsureQueries()
        {
            var world = World.DefaultGameObjectInjectionWorld;

            if (world is { IsCreated: false })
            {
                m_QueriesReady = false;
                return false;
            }

            if (m_QueriesReady)
            {
                return true;
            }

            var entityManager = world.EntityManager;
            m_ConfigQuery = entityManager.CreateEntityQuery(ComponentType.ReadWrite<SimulationConfig>());
            m_LimitsQuery = entityManager.CreateEntityQuery(ComponentType.ReadOnly<SimulationLimits>());
            m_StatsQuery = entityManager.CreateEntityQuery(ComponentType.ReadOnly<SimulationStats>());
            m_QueriesReady = true;

            return true;
        }

        private void ReleaseQueries()
        {
            if (!m_QueriesReady)
                return;

            var world = World.DefaultGameObjectInjectionWorld;
            if (world != null && world.IsCreated)
            {
                m_ConfigQuery.Dispose();
                m_LimitsQuery.Dispose();
                m_StatsQuery.Dispose();
            }

            m_QueriesReady = false;
        }

        /// <summary>Shows the authored value once the sub scene has finished streaming in.</summary>
        private void SyncFieldWithSimulation()
        {
            if (m_MaxBotsField == null || m_MaxBotsField.isFocused || m_ConfigQuery.IsEmptyIgnoreFilter)
                return;

            var current = m_ConfigQuery.GetSingleton<SimulationConfig>().MaxBotCount;
            if (current == m_LastAppliedMax)
                return;

            m_LastAppliedMax = current;
            m_MaxBotsField.SetTextWithoutNotify(current.ToString(CultureInfo.InvariantCulture));
        }

        private void ApplyFromField()
        {
            // Silently ignored until the sub scene has finished streaming in and the
            // config singleton actually exists.
            if (m_MaxBotsField == null || !EnsureQueries() || m_ConfigQuery.IsEmptyIgnoreFilter)
            {
                return;
            }

            if (!int.TryParse(m_MaxBotsField.text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var requested))
            {
                requested = m_LastAppliedMax < 0 ? 0 : m_LastAppliedMax;
            }

            var hardMax = m_LimitsQuery.IsEmptyIgnoreFilter
                ? 200000
                : m_LimitsQuery.GetSingleton<SimulationLimits>().HardMaxBotCount;

            requested = Mathf.Clamp(requested, 0, hardMax);

            var config = m_ConfigQuery.GetSingleton<SimulationConfig>();
            config.MaxBotCount = requested;
            m_ConfigQuery.SetSingleton(config);

            m_LastAppliedMax = requested;
            m_MaxBotsField.SetTextWithoutNotify(requested.ToString(CultureInfo.InvariantCulture));
        }

        private void RefreshStats()
        {
            if (m_StatsText == null)
                return;

            m_SmoothedDeltaTime = Mathf.Lerp(m_SmoothedDeltaTime, Time.unscaledDeltaTime, 0.1f);
            var fps = m_SmoothedDeltaTime > 0.0001f ? 1f / m_SmoothedDeltaTime : 0f;

            if (m_StatsQuery.IsEmptyIgnoreFilter)
            {
                m_StatsText.text = "streaming sub scene...";
                return;
            }

            var stats = m_StatsQuery.GetSingleton<SimulationStats>();
            m_StatsText.text = string.Format(
                CultureInfo.InvariantCulture,
                "alive bots   {0}\ndestroyed    {1}\nthis frame   {2}\nfps          {3:0.}",
                stats.AliveBotCount, stats.DestroyedBotCount, stats.DestroyedThisFrame, fps);
        }
    }
}