using Unity.Entities;
using Unity.Mathematics;

namespace GalaxyIO.DOTS.Client.Components.Message
{
    /// <summary>
    /// 우주선 파괴 연출 요청. 클라이언트 전용. <see cref="Systems.SpaceshipDestructionReceiveSystem"/>이 만들고
    /// 크기별 파괴 연출 뷰(DestructionFxView)가 읽고 지움.
    /// </summary>
    public struct SpaceshipDestroyedFxMessage : IComponentData
    {
        public float2 Position;

        // 터진 우주선의 선체 반지름(두께 / 2). 찾지 못했으면 0.
        public float Radius;

        // 진행 방향 속도. 파편이 물려받음.
        public float2 Velocity;

        // 내 우주선이 터졌는지, 내가 격추했는지
        public bool Local;
        public bool KilledByLocal;
    }
}
