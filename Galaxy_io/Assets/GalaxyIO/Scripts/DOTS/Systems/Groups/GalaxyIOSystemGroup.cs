using Unity.Entities;
using Unity.NetCode;
using Unity.Transforms;

namespace GalaxyIO.DOTS.Systems
{
    /// <summary>
    /// 틱의 첫 단계. 입력, AI, 컨텐츠가 속도를 정하고 스폰 메시지를 남기는 그룹.
    /// <see cref="LocalTransform"/>은 쓰지 않음.
    /// </summary>
    [UpdateInGroup(typeof(PredictedSimulationSystemGroup))]
    [UpdateBefore(typeof(MovementSystemGroup))]
    public partial class UpdateSystemGroup : ComponentSystemGroup
    {
    }

    /// <summary>
    /// AI 의 인지와 조종을 담당하는 그룹. 조종 결과는 <see cref="MovementSystemGroup"/>이 위치에 반영함.
    /// </summary>
    [WorldSystemFilter(WorldSystemFilterFlags.ServerSimulation)]
    [UpdateInGroup(typeof(UpdateSystemGroup))]
    public partial class AISystemGroup : ComponentSystemGroup
    {
    }

    /// <summary>
    /// <see cref="Components.Actor.MovementComponent"/>를 위치와 회전에 반영하는 그룹.
    /// </summary>
    [UpdateInGroup(typeof(PredictedSimulationSystemGroup))]
    public partial class MovementSystemGroup : ComponentSystemGroup
    {
    }

    /// <summary>
    /// 스폰 메시지를 받아 Entity 를 만드는 그룹. 스폰한 쪽의 이동이 끝난 위치를 쓰도록
    /// <see cref="MovementSystemGroup"/> 다음에 실행함.
    /// </summary>
    [UpdateInGroup(typeof(PredictedSimulationSystemGroup))]
    [UpdateAfter(typeof(MovementSystemGroup))]
    public partial class SpawnSystemGroup : ComponentSystemGroup
    {
    }

    /// <summary>
    /// 충돌체끼리의 겹침을 찾아 메시지만 남기는 그룹.
    /// </summary>
    [UpdateInGroup(typeof(PredictedSimulationSystemGroup))]
    [UpdateAfter(typeof(SpawnSystemGroup))]
    public partial class CollisionDetectionSystemGroup : ComponentSystemGroup
    {
    }

    /// <summary>
    /// 충돌 메시지를 처리하는 그룹. 밀어내기, 데미지, 줍기, 파괴 등.
    /// 투사체 소멸은 예측 대상이라 틱 단위로 돌아야 함.
    /// </summary>
    [UpdateInGroup(typeof(PredictedSimulationSystemGroup))]
    [UpdateAfter(typeof(CollisionDetectionSystemGroup))]
    public partial class EventSystemGroup : ComponentSystemGroup
    {
    }
}
