using GalaxyIO.DOTS.Components.Singleton;
using Unity.Collections;
using Unity.Entities;

namespace GalaxyIO.DOTS.Components.Message
{
    /// <summary>
    /// 팀 모드에서 플레이어 우주선이 격추됨. GameModeSystem 이 읽고 제거함. 서버 전용.
    /// </summary>
    public struct DeathMessage : IComponentData
    {
        public int Victim;

        // 0 이면 AI 막타이거나 알 수 없음.
        public int Killer;

        // 막타 이름. 플레이어 이름 또는 AI 표시 이름. 알 수 없으면 비어 있음.
        public FixedString64Bytes KillerName;

        // 0 이면 어시스트 없음.
        public int Assistant;

        public SpaceshipId Spaceship;
    }
}
