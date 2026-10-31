using GalaxyIO.DOTS.Match;
using Unity.Entities;

namespace GalaxyIO.DOTS.Components.Singleton
{
    /// <summary>
    /// <see cref="GameModeService"/>를 시스템에 전달하는 컴포넌트. 팀 모드 서버 월드에만 한 개.
    /// </summary>
    public sealed class GameModeServiceComponent : IComponentData
    {
        public GameModeService Value;
    }
}
