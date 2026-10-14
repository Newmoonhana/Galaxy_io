using Unity.Entities;

namespace GalaxyIO.DOTS.Components.Singleton
{
    /// <summary>
    /// <see cref="SpaceshipId"/>로 우주선 프리팹 Entity 를 찾는 레지스트리. 월드당 한 개.
    /// 버퍼 인덱스가 곧 <see cref="SpaceshipId"/> 값임. 나머지 정의는 <see cref="SpaceshipRegistryAssetComponent"/>에서 찾음.
    /// </summary>
    [InternalBufferCapacity(0)]
    public struct SpaceshipRegistryElement : IBufferElementData
    {
        // 베이킹된 프리팹 Entity. 프리팹이 없는 기종은 Entity.Null.
        public Entity Prefab;
    }

    public static class SpaceshipRegistry
    {
        /// <summary>
        /// 프리팹이 없거나 등록되지 않은 Id 는 <see cref="Entity.Null"/>을 돌려줌.
        /// </summary>
        public static Entity Find(this DynamicBuffer<SpaceshipRegistryElement> registry, SpaceshipId id)
        {
            return (int)id < registry.Length ? registry[(int)id].Prefab : Entity.Null;
        }
    }
}
