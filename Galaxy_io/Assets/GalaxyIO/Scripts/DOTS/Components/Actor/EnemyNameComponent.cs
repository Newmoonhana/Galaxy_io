using Unity.Collections;
using Unity.Entities;

namespace GalaxyIO.DOTS.Components.Actor
{
    /// <summary>
    /// AI 우주선의 표시 이름. 킬 로그에 씀.
    /// </summary>
    public struct EnemyNameComponent : IComponentData
    {
        public FixedString32Bytes Value;
    }
}
