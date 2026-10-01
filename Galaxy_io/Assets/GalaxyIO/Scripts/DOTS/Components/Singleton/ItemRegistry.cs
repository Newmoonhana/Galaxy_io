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

        // 포탑만. 총구 순서대로 놓인 TurretSpecElement 범위.
        public ushort TurretSpecStart;
        public byte TurretSpecCount;

        // 설계도만. 제작 결과물과 RecipeIngredientElement 범위. 설계도가 아니면 None.
        public ItemId CraftResult;
        public ushort IngredientStart;
        public byte IngredientCount;
    }

    /// <summary>
    /// 포탑 아이템의 총구 하나의 사양. 레지스트리 Entity 에 모든 포탑 것이 이어 붙어 있음.
    /// </summary>
    [InternalBufferCapacity(0)]
    public struct TurretSpecElement : IBufferElementData
    {
        // 없으면 이 총구는 쏘지 않음.
        public Entity ProjectilePrefab;

        public float MaxFireCooldown;

        // 탄 퍼짐 정도(Radian)
        public float Spread;

        // 투사체 공격력
        public float Damage;

        public UnityObjectRef<AudioClip> ShotSound;
    }

    /// <summary>
    /// 설계도 제작 재료 하나. 레지스트리 Entity 에 모든 설계도 것이 이어 붙어 있음.
    /// </summary>
    [InternalBufferCapacity(0)]
    public struct RecipeIngredientElement : IBufferElementData
    {
        public ItemId Id;

        public int Count;
    }

    public static class ItemRegistry
    {
        public static bool Contains(this DynamicBuffer<ItemRegistryElement> registry, ItemId id)
        {
            return id != ItemId.None
                   && (int)id < registry.Length
                   && registry[(int)id].Sprite != default(UnityObjectRef<Sprite>);
        }

        /// <summary>
        /// 포탑의 muzzle 번째 총구 사양. 포탑이 아니거나 그 총구 사양이 없으면 default (쏘지 않음).
        /// </summary>
        public static TurretSpecElement GetTurretSpec(
            this DynamicBuffer<ItemRegistryElement> registry,
            DynamicBuffer<TurretSpecElement> specs,
            ItemId turret,
            int muzzle)
        {
            if (!registry.Contains(turret))
            {
                return default;
            }

            ItemRegistryElement item = registry[(int)turret];
            return muzzle < item.TurretSpecCount ? specs[item.TurretSpecStart + muzzle] : default;
        }
    }
}
