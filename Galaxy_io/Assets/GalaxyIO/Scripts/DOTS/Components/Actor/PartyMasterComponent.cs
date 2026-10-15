using Unity.Entities;
using Unity.NetCode;

namespace GalaxyIO.DOTS.Components.Actor
{
    /// <summary>
    /// 플레이어 우주선의 주인이 파티장인지. 서버의 PartyService 가 채우고, 클라이언트는 왕관 표시에 씀.
    /// </summary>
    public struct PartyMasterComponent : IComponentData
    {
        [GhostField]
        public bool Value;
    }
}