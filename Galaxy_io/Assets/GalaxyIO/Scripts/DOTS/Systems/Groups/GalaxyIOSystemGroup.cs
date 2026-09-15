using Unity.Entities;
using Unity.NetCode;

namespace GalaxyIO.DOTS.Systems
{
    /// <summary>
    /// AI 의 인지와 조종을 담당하는 그룹. 조종 결과는 <see cref="MovementSystemGroup"/>이 위치에 반영함.
    /// </summary>
    [WorldSystemFilter(WorldSystemFilterFlags.ServerSimulation)]
    [UpdateInGroup(typeof(PredictedSimulationSystemGroup))]
    [UpdateBefore(typeof(MovementSystemGroup))]
    public partial class AISystemGroup : ComponentSystemGroup
    {
    }

    /// <summary>
    /// 월드 내 이동 가능한 오브젝트들을 옮기는 그룹.
    /// </summary>
    [UpdateInGroup(typeof(PredictedSimulationSystemGroup))]
    public partial class MovementSystemGroup : ComponentSystemGroup
    {
    }

    /// <summary>
    /// 몹, 투사체, 아이템 등 월드에 새 오브젝트를 추가하는 스폰 시스템 그룹.
    /// 다른 Entity의 Transform을 기준으로 생성되므로
    /// <see cref="MovementSystemGroup"/> 다음에 실행함.
    /// </summary>
    [UpdateInGroup(typeof(PredictedSimulationSystemGroup))]
    [UpdateAfter(typeof(MovementSystemGroup))]
    public partial class SpawnSystemGroup : ComponentSystemGroup
    {
    }

    /// <summary>
    /// 충돌체를 가지고 있는 물체간의 충돌을 처리하는 시스템 그룹.
    /// </summary>
    [UpdateInGroup(typeof(PredictedSimulationSystemGroup))]
    [UpdateAfter(typeof(MovementSystemGroup))]
    public partial class CollisionDetectionSystemGroup : ComponentSystemGroup
    {
    }

    /// <summary>
    /// 오브젝트를 파괴하는 시스템 그룹. (e.g. 투사체 지속시간 만료)
    /// 투사체 소멸은 예측 대상이라 틱 단위로 돌아야 함.
    /// </summary>
    [UpdateInGroup(typeof(PredictedSimulationSystemGroup))]
    [UpdateAfter(typeof(CollisionDetectionSystemGroup))]
    public partial class DespawnSystemGroup : ComponentSystemGroup
    {
    }
}