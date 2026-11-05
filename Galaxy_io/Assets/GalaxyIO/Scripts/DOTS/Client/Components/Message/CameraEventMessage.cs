using Unity.Entities;
using Unity.Mathematics;

namespace GalaxyIO.DOTS.Client.Components.Message
{
    public enum CameraEventKind : byte
    {
        // 우주선 파괴. 위치는 파괴된 우주선.
        ShipDestroyed,

        // 범위 피해 투사체 명중. 위치는 명중 지점.
        ProjectileExplosion,
    }

    /// <summary>
    /// 카메라 연출용 이벤트 한 건. CameraEventDirector 가 받아 제거함.
    /// </summary>
    public struct CameraEventMessage : IComponentData
    {
        public CameraEventKind Kind;

        public float2 Position;

        // ProjectileExplosion 의 피해 반경
        public float Radius;

        // ShipDestroyed 의 주인 / 막타 NetworkId. AI 또는 알 수 없으면 0 이하.
        public int OwnerNetworkId;
        public int KillerNetworkId;
    }
}
