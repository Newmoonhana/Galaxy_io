using Unity.Entities;

namespace GalaxyIO.DOTS.Components.Actor
{
    /// <summary>
    /// 발사 연출/발사음을 아직 내지 않은 투사체 표시. 클라이언트가 처리 후 끔.
    /// </summary>
    public struct ShotPendingTag : IComponentData, IEnableableComponent
    {
    }
}
