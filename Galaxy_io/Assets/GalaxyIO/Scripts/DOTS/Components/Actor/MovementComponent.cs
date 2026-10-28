using GalaxyIO.DOTS.Systems;
using Unity.Entities;
using Unity.Mathematics;
using Unity.NetCode;

namespace GalaxyIO.DOTS.Components.Actor
{
    /// <summary>
    /// 우주선, 투사체, 아이템이 이번 틱에 움직이는 양. Update 단계의 시스템이 값을 정하고
    /// <see cref="MovementSystem"/>이 Transform 에 반영함.
    /// 예측하는 클라이언트에만 복제함. 다른 클라이언트는 위치와 회전만 받아 보간함.
    /// </summary>
    [GhostComponent(SendTypeOptimization = GhostSendType.OnlyPredictedClients)]
    public struct MovementComponent : IComponentData
    {
        // 월드 좌표 상 초당 이동 벡터. 플레이어의 비행 모델은 이 값을 이어서 고침.
        // 예측 롤백 시 서버 값으로 되감아야 하므로 복제함.
        [GhostField]
        public float2 Velocity;

        // 초당 회전량(라디안). +는 반시계 방향. 매 틱 다시 정하므로 복제하지 않음.
        public float AngularVelocity;
    }
}
