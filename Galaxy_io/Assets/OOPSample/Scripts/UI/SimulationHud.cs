using System.Globalization;
using UnityEngine;
using UnityEngine.UI;

namespace OOPSample
{
    /// <summary>
    /// ECS 샘플의 <c>SimulationHud</c> 대응.
    ///
    /// 원본은 GameObject 월드와 엔티티 월드 사이의 다리였습니다. MonoBehaviour가 시스템을
    /// 호출할 수 없으므로 <c>World.DefaultGameObjectInjectionWorld</c>를 잡아 EntityQuery를
    /// 캐싱하고, 싱글톤 컴포넌트를 읽고 쓰는 방식으로만 대화할 수 있었죠.
    ///
    /// OOP 버전에는 건널 경계가 없습니다. <see cref="SimulationRunner"/> 참조 하나면 끝입니다.
    /// 이 파일이 원본의 절반도 안 되는 이유가 그것이고, 반대로 말하면 DOTS의 그 배선이
    /// 전부 "월드가 둘"이라는 사실 때문에 존재했다는 뜻이기도 합니다.
    /// </summary>
    [AddComponentMenu("OOP Simulator/Simulation HUD")]
    [DefaultExecutionOrder(500)]
    public class SimulationHud : MonoBehaviour
    {
        [SerializeField]
        private SimulationRunner m_Runner;

        [SerializeField]
        private InputField m_MaxBotsField;

        [SerializeField]
        private Button m_ApplyButton;

        [SerializeField]
        private Text m_StatsText;

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

            PlayerInputReader.UiHasFocus = false;
        }

        private void Update()
        {
            // While the field has keyboard focus, WASD belongs to the text box, not the player.
            PlayerInputReader.UiHasFocus = m_MaxBotsField != null && m_MaxBotsField.isFocused;

            SyncFieldWithSimulation();
        }

        /// <summary>
        /// 컨슈머가 <c>SimulationRunner.LateUpdate</c>에서 돌기 때문에, 카운터는 그 뒤에 읽습니다.
        /// (Runner의 실행 순서가 -1000이라 이 컴포넌트의 LateUpdate보다 먼저 호출됩니다.)
        /// </summary>
        private void LateUpdate()
        {
            RefreshStats();
        }

        private void SyncFieldWithSimulation()
        {
            if (m_Runner == null || m_MaxBotsField == null || m_MaxBotsField.isFocused)
                return;

            var current = m_Runner.MaxBotCount;
            if (current == m_LastAppliedMax)
                return;

            m_LastAppliedMax = current;
            m_MaxBotsField.SetTextWithoutNotify(current.ToString(CultureInfo.InvariantCulture));
        }

        private void ApplyFromField()
        {
            if (m_Runner == null || m_MaxBotsField == null)
                return;

            if (!int.TryParse(m_MaxBotsField.text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var requested))
            {
                requested = m_LastAppliedMax < 0 ? 0 : m_LastAppliedMax;
            }

            requested = Mathf.Clamp(requested, 0, m_Runner.HardMaxBotCount);

            m_Runner.MaxBotCount = requested;
            m_LastAppliedMax = requested;
            m_MaxBotsField.SetTextWithoutNotify(requested.ToString(CultureInfo.InvariantCulture));
        }

        private void RefreshStats()
        {
            if (m_StatsText == null)
                return;

            m_SmoothedDeltaTime = Mathf.Lerp(m_SmoothedDeltaTime, Time.unscaledDeltaTime, 0.1f);
            var fps = m_SmoothedDeltaTime > 0.0001f ? 1f / m_SmoothedDeltaTime : 0f;

            if (m_Runner == null)
            {
                m_StatsText.text = "no simulation runner";
                return;
            }

            var stats = m_Runner.Stats;
            m_StatsText.text = string.Format(
                CultureInfo.InvariantCulture,
                "alive bots   {0}\ndestroyed    {1}\nthis frame   {2}\nfps          {3:0.}",
                stats.AliveBotCount, stats.DestroyedBotCount, stats.DestroyedThisFrame, fps);
        }
    }
}
