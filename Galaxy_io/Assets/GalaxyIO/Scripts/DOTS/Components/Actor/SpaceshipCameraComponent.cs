using GalaxyIO.DOTS.Authoring;
using Unity.Entities;

namespace GalaxyIO.DOTS.Components.Actor
{
    /// <summary>
    /// <see cref="SpaceshipAuthoring"/>의 카메라 값. 프리팹에 베이킹되므로 복제하지 않음. 플레이어 우주선만.
    /// </summary>
    public struct SpaceshipCameraComponent : IComponentData
    {
        // 가장 멀리 축소했을 때의 시야 크기(Orthographic Size)
        public float MaxOrthographicSize;
    }
}
