using Unity.Entities;

namespace GalaxyIO.DOTS.Components.Singleton
{
    /// <summary>
    /// <see cref="PrefabId"/>로 프리팹 Entity 를 찾는 레지스트리. 월드당 한 개.
    /// 버퍼 인덱스가 곧 <see cref="PrefabId"/> 값임.
    /// </summary>
    [InternalBufferCapacity(8)]
    public struct PrefabRegistryElement : IBufferElementData
    {
        // 베이킹된 프리팹 Entity. 빈 칸이면 Entity.Null. 월드마다 값이 다름.
        public Entity Prefab;
    }

    public static class PrefabRegistry
    {
        /// <summary>
        /// 등록되지 않은 Id 는 <see cref="Entity.Null"/>을 돌려줌.
        /// </summary>
        public static Entity Find(this DynamicBuffer<PrefabRegistryElement> registry, PrefabId id)
        {
            return (int)id < registry.Length ? registry[(int)id].Prefab : Entity.Null;
        }

        public static bool Contains(this DynamicBuffer<PrefabRegistryElement> registry, PrefabId id)
        {
            return registry.Find(id) != Entity.Null;
        }
    }
}