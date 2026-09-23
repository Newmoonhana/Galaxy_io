using Unity.Entities;

namespace GalaxyIO.DOTS.Components.Actor
{
    /// <summary>
    /// 발사음을 아직 재생하지 않은 투사체 표시. 클라이언트가 재생 후 끔.
    /// </summary>
    public struct ShotSoundPendingTag : IComponentData, IEnableableComponent
    {
    }
}
