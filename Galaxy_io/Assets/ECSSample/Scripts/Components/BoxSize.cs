using Unity.Entities;

namespace GalaxyIO
{
    /// <summary>
    /// AABB 충돌체. <see cref="Value"/>는 월드 좌표계상의 박스의 크기. (그러므로 항상 정사각형)
    /// <c>LocalTransform.Scale</c>에 곱해져서 크기 결정
    /// </summary>
    public struct BoxSize : IComponentData
    {
        public float Value;
    }
}