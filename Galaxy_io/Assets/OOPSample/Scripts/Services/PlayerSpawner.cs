using UnityEngine;

namespace OOPSample
{
    /// <summary>
    /// ECS 샘플의 <c>PlayerSpawnSystem</c> 대응. 플레이어 박스가 정확히 하나 존재하도록 합니다.
    /// </summary>
    public sealed class PlayerSpawner
    {
        private readonly SimulationConfig m_Config;
        private readonly PlayerInputReader m_Input;
        private readonly WorldBounds m_Bounds;
        private readonly Transform m_Parent;

        public PlayerBehaviour Player { get; private set; }

        public PlayerSpawner(SimulationConfig config, PlayerInputReader input, in WorldBounds bounds, Transform parent)
        {
            m_Config = config;
            m_Input = input;
            m_Bounds = bounds;
            m_Parent = parent;
        }

        public void Tick()
        {
            if (Player != null || m_Config.PlayerPrefab == null)
                return;

            Player = Object.Instantiate(m_Config.PlayerPrefab, m_Parent);
            Player.name = "Player";
            Player.Initialize(m_Input, m_Bounds, m_Config.PlayerSize, m_Config.PlayerSpeed);
        }
    }
}
