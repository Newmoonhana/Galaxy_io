using Unity.Entities;
using Unity.Mathematics;

namespace GalaxyIO.DOTS.Components.Actor
{
    /// <summary>
    /// 외부 시스템이 AI 상태 머신에 전달하는 이벤트 종류.
    /// </summary>
    public enum EnemyEventID : byte
    {
        // 투사체에 피격됨
        ProjectileHit = 0,
    }

    /// <summary>
    /// AI 상태 머신의 이벤트 큐. 외부 시스템이 쌓고 행동 시스템이 매 틱 비움.
    /// </summary>
    [InternalBufferCapacity(2)]
    public struct EnemyEventElement : IBufferElementData
    {
        public EnemyEventID ID;

        // 이벤트를 일으킨 우주선
        public Entity Instigator;

        // 이벤트 발생 시점의 Instigator 위치
        public float2 InstigatorPosition;
    }
}