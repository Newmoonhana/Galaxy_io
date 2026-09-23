using Unity.Collections;
using Unity.Entities;

namespace GalaxyIO.DOTS.Components.Actor
{
    /// <summary>
    /// 접속이 끊겨도 Connection 에 남는 플레이어 이름. 퇴장 로그용. 서버 전용.
    /// </summary>
    public struct PlayerNameCleanup : ICleanupComponentData
    {
        public FixedString64Bytes Value;
    }
}
