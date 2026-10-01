using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

namespace GalaxyIO.DOTS.Components.Singleton
{
    /// <summary>
    /// <see cref="ItemId"/>로 아이템 정의를 찾는 레지스트리. 월드당 한 개.
    /// 버퍼 인덱스가 곧 <see cref="ItemId"/> 값임.
    /// </summary>
    [InternalBufferCapacity(8)]
    public struct ItemRegistryElement : IBufferElementData
    {
        // 월드에 떨어진 아이템 Entity 의 스프라이트. 빈 칸이면 null.
        public UnityObjectRef<Sprite> Sprite;

        // 스프라이트에 맞춘 줍기 충돌체 (반너비, 반높이)
        public float2 HalfExtents;

        public ItemType Type;

        // 인벤토리 한 칸에 쌓을 수 있는 최대 개수 (1 ~ 255)
        public byte MaxStack;
    }

    public static class ItemRegistry
    {
        public static bool Contains(this DynamicBuffer<ItemRegistryElement> registry, ItemId id)
        {
            return id != ItemId.None
                   && (int)id < registry.Length
                   && registry[(int)id].Sprite != default(UnityObjectRef<Sprite>);
        }
    }
}
