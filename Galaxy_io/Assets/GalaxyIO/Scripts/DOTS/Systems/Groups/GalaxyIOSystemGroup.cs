using Unity.Entities;
using Unity.NetCode;

namespace GalaxyIO.DOTS.Systems
{
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
    public partial class GalaxyIOSystemGroup : ComponentSystemGroup
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
    /// 예측 루프 안에서 파괴한 Entity는 롤백으로 되살릴 수 없어 루프 밖에 둠.
    /// <see cref="PredictedSimulationSystemGroup"/>이 OrderFirst 그룹이라
    /// 별도 [UpdateAfter] 없이도 항상 그 뒤에 실행됨.
    /// </summary>
    public partial class DespawnSystemGroup : ComponentSystemGroup
    {
    }
}