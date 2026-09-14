using GalaxyIO.DOTS.Authoring;
using Unity.Entities;
using Unity.Mathematics;

namespace GalaxyIO.DOTS.Components.Actor
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

        // 로컬 +Y 를 향한 부채꼴. 시야 전용. 부채꼴끼리는 판정하지 않음.
        Sector = 2,
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

        // Obb: (반너비, 반높이). Circle: x가 반지름, y는 사용하지 않음. Sector: (반지름, 반각 라디안).
        public float2 Extents;
    }
}