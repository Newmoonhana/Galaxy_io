using Unity.Entities;

namespace GalaxyIO.DOTS.Components.Actor
{
    /// <summary>
    /// 플레이어 점수. 서버의 Connection 에 붙음.
    /// </summary>
    public struct PlayerScoreComponent : IComponentData
    {
        public int Value;
    }
}
