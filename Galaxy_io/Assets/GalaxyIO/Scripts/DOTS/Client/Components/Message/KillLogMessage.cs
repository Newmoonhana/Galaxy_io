using GalaxyIO.DOTS.Components.Actor;
using Unity.Collections;
using Unity.Entities;

namespace GalaxyIO.DOTS.Client.Components.Message
{
    /// <summary>
    /// 화면에 띄울 킬 로그 한 줄. 킬 로그 뷰가 읽고 제거함.
    /// </summary>
    public struct KillLogMessage : IComponentData
    {
        public FixedString64Bytes Killer;

        // 어시스트. 없으면 비어 있음.
        public FixedString64Bytes Assistant;

        public FixedString64Bytes Victim;

        public KillType Type;

        // 막타가 AI 인지
        public bool KillerIsEnemy;

        // 로컬 플레이어가 막타/어시스트/격추 대상 중 하나인지
        public bool IsLocal;
    }
}
