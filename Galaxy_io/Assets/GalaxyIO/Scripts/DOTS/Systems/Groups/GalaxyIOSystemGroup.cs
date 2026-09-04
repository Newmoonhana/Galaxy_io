using Unity.Entities;

namespace GalaxyIO.DOTS.Systems
{
    /// <summary>
    /// 월드 내 이동 가능한 오브젝트들을 옮기는 그룹.
    /// </summary>
    public partial class MovementSystemGroup : ComponentSystemGroup
    {
    }

    /// <summary>
    /// 몹, 투사체, 아이템 등 월드에 새 오브젝트를 추가하는 스폰 시스템 그룹.
    /// 다른 Entity의 Transform을 기준으로 생성되므로
    /// <see cref="MovementSystemGroup"/> 다음에 실행함.
    /// </summary>
    [UpdateAfter(typeof(MovementSystemGroup))]
    public partial class GalaxyIOSystemGroup : ComponentSystemGroup
    {
    }

    /// <summary>
    /// 충돌체를 가지고 있는 물체간의 충돌을 처리하는 시스템 그룹.
    /// </summary>
    [UpdateAfter(typeof(MovementSystemGroup))]
    public partial class CollisionDetectionSystemGroup : ComponentSystemGroup
    {
    }

    /// <summary>
    /// 오브젝트를 파괴하는 시스템 그룹. (e.g. 투사체 지속시간 만료)
    /// </summary>
    [UpdateAfter(typeof(CollisionDetectionSystemGroup))]
    public partial class DespawnSystemGroup : ComponentSystemGroup
    {
    }
}