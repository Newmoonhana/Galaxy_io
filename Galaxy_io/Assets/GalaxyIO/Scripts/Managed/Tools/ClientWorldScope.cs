using Unity.Entities;
using Unity.NetCode;

namespace GalaxyIO.Tools
{
    /// <summary>
    /// 캐시해 둔 <see cref="EntityQuery"/>를 쓰기 전에 그 월드가 아직 살아 있는지 확인하는 도우미.
    /// <p/>
    /// 세션을 나가면 <c>GameSession</c>이 클라이언트 월드를 Dispose 함.
    /// 인게임 씬이 내려가기까지 몇 프레임이 걸리므로 그 사이에 캐시한 쿼리를 쓰면 예외가 남.
    /// </summary>
    public static class ClientWorldScope
    {
        /// <summary>
        /// 캐시해 둔 월드가 여전히 현재 클라이언트 월드로 살아 있는지 여부.
        /// </summary>
        public static bool IsAlive(World world)
        {
            return world != null && world.IsCreated && world == ClientServerBootstrap.ClientWorld;
        }
    }
}
