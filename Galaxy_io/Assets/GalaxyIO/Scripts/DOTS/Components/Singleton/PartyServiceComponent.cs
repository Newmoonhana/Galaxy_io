using GalaxyIO.DOTS.Party;
using Unity.Entities;

namespace GalaxyIO.DOTS.Components.Singleton
{
    /// <summary>
    /// <see cref="PartyService"/>를 시스템에 전달하는 컴포넌트. 서버 월드당 한 개.
    /// </summary>
    public sealed class PartyServiceComponent : IComponentData
    {
        public PartyService Value;
    }
}