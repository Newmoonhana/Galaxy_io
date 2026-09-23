using Unity.Collections;
using Unity.Entities;

namespace GalaxyIO.DOTS.Components.Actor
{
    /// <summary>
    /// 격추 방식. 킬 로그 아이콘을 고르는 데 씀.
    /// </summary>
    public enum KillType : byte
    {
        None = 0,

        // 투사체
        Indirect = 1,

        // 선체 충돌
        Ram = 2,
    }

    /// <summary>
    /// 우주선의 체력을 0으로 만든 타격. 서버만 채움.
    /// </summary>
    public struct KillingBlowComponent : IComponentData
    {
        public KillType Type;

        // 막타를 친 쪽. AI 는 -1.
        public int NetworkId;

        // 막타를 친 AI 의 표시 이름. 플레이어면 비어 있음.
        public FixedString32Bytes EnemyName;
    }
}
