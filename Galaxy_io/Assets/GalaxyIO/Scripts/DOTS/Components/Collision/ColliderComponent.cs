using GalaxyIO.DOTS.Authoring;
using Unity.Entities;
using Unity.Mathematics;

namespace GalaxyIO.DOTS.Components.Collision
{
    /// <summary>
    /// 충돌체의 모양.
    /// </summary>
    public enum ColliderShape : byte
    {
        // 오브젝트와 함께 회전하는 직사각형
        Obb = 0,

        // 원
        Circle = 1,
    }

    /// <summary>
    /// <see cref="ColliderAuthoring"/>의 DOTS 구조체. 모든 셰이프가 이 한 구조체를 공유함.
    /// 셰이프별로 컴포넌트를 나누지 않아 아키타입이 쪼개지지 않음.
    /// </summary>
    public struct ColliderComponent : IComponentData
    {
        // 셰이프. 이 값에 따라 Extents의 의미가 달라짐.
        public ColliderShape Shape;

        // Entity 기준 로컬 중심 오프셋
        public float2 Center;

        // Obb: (반너비, 반높이). Circle: x가 반지름, y는 사용하지 않음.
        public float2 Extents;
    }
}