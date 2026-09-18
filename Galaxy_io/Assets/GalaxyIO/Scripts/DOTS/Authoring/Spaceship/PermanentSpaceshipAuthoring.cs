using GalaxyIO.DOTS.Components.Actor;
using Unity.Entities;
using UnityEngine;

namespace GalaxyIO.DOTS.Authoring
{
    /// <summary>
    /// 레이더 밖에서도 제거되지 않는 우주선을 나타내는 컴포넌트. (e.g. 이벤트 몬스터)
    /// </summary>
    public sealed class PermanentSpaceshipAuthoring : MonoBehaviour
    {
    }

    public sealed class PermanentSpaceshipBaker : Baker<PermanentSpaceshipAuthoring>
    {
        public override void Bake(PermanentSpaceshipAuthoring authoring)
        {
            Entity entity = GetEntity(TransformUsageFlags.Dynamic);
            AddComponent<PermanentSpaceshipTag>(entity);
        }
    }
}
