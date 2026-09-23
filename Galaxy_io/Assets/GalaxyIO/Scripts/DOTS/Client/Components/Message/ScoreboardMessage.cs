using Unity.Collections;
using Unity.Entities;

namespace GalaxyIO.DOTS.Client.Components.Message
{
    /// <summary>
    /// 점수판에 반영할 플레이어 한 명의 점수. 점수판 뷰가 읽고 제거함.
    /// </summary>
    public struct ScoreboardMessage : IComponentData
    {
        public FixedString64Bytes Name;

        public int NetworkId;

        public int Score;

        // 퇴장 여부. true 면 점수판에서 뺌.
        public bool Left;
    }
}
