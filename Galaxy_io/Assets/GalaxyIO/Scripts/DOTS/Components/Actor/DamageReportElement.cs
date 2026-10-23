using GalaxyIO.DOTS.Components.Singleton;
using Unity.Entities;
using Unity.Mathematics;

namespace GalaxyIO.DOTS.Components.Actor
{
    /// <summary>
    /// 이번 틱에 우주선이 받은 데미지. 맞을 때마다 하나씩 쌓임. 서버만 채우고 DamageNotifySystem 이 보낸 뒤 비움.
    /// </summary>
    [InternalBufferCapacity(0)]
    public struct DamageReportElement : IBufferElementData
    {
        // 때린 플레이어. AI 는 -1.
        public int AttackerNetworkId;

        public float Amount;

        // 맞은 지점
        public float2 Position;

        // 맞힌 포탑. 충돌 데미지는 None.
        public ItemId Turret;
    }
}
