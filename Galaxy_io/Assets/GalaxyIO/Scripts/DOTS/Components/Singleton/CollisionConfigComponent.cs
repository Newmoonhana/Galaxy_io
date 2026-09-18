using GalaxyIO.DOTS.Authoring.Config;
using Unity.Entities;

namespace GalaxyIO.DOTS.Components.Singleton
{
    /// <summary>
    /// <see cref="CollisionConfigAuthoring"/>의 DOTS 구조체. 월드당 한 개.
    /// </summary>
    public struct CollisionConfigComponent : IComponentData
    {
        // 격자 칸 한 변 길이
        public float CellSize;

        // 되감기 여유분을 잡을 때 쓰는 우주선 최대 속력
        public float RewindSpeed;

        // 선체끼리 부딪혔을 때 튕겨 나가는 정도. 0이면 미끄러짐.
        public float Restitution;
    }
}
