using Unity.Entities;

namespace GalaxyIO.Systems
{
    /// <summary>
    /// 몹, 투사체, 아이템 등 월드 내에 새로운 오브젝트를 추가하는 스폰 시스템 그룹
    /// </summary>
    public partial class GalaxyIOSystemGroup : ComponentSystemGroup
    {
    }

    /// <summary>
    /// 월드 내 이동 가능한 오브젝트들을 옮기는 그룹
    /// </summary>
    [UpdateAfter(typeof(GalaxyIOSystemGroup))]
    public partial class MovementSystemGroup : ComponentSystemGroup
    {
    }

    /// <summary>
    /// 충돌체를 가지고 있는 물체간의 충돌을 처리하는 시스템 그룹
    /// </summary>
    [UpdateAfter(typeof(MovementSystemGroup))]
    public partial class CollisionDetectionSystemGroup : ComponentSystemGroup
    {
    }

    /// <summary>
    /// 오브젝트를 파괴하는 시스템 그룹 (e.g. 투사체 지속시간 만료)
    /// </summary>
    [UpdateAfter(typeof(CollisionDetectionSystemGroup))]
    public partial class DespawnSystemGroup : ComponentSystemGroup
    {
    }
}