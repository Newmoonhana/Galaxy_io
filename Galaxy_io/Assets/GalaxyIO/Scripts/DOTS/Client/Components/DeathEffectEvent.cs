using Unity.Entities;
using Unity.Mathematics;

namespace GalaxyIO.DOTS.Client.Components
{
    /// <summary>
    /// 폭발 연출을 띄울 위치. 연출을 재생한 MonoBehaviour 가 제거함.
    /// </summary>
    public struct DeathEffectEvent : IComponentData
    {
        public float2 Position;
    }
}
