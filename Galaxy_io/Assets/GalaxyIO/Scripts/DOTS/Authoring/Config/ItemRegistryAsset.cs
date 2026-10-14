using System;
using System.Collections.Generic;
using GalaxyIO.DOTS.Components.Singleton;
using UnityEngine;

namespace GalaxyIO.DOTS.Authoring.Config
{
    /// <summary>
    /// 게임에 등장하는 아이템 목록. <see cref="PrefabRegistryAuthoring"/>를 통해 베이킹됨.
    /// </summary>
    [CreateAssetMenu(fileName = "SO_ItemRegistry", menuName = "GalaxyIO/Item Registry")]
    public sealed class ItemRegistryAsset : ScriptableObject
    {
        [SerializeField]
        [Tooltip("ItemId 로 찾을 아이템 목록. 순서는 상관없음.")]
        public List<ItemAsset> Items = new List<ItemAsset>();

        [SerializeField]
        [Tooltip("포탑 상점 목록. 이 순서대로 보여줌.")]
        public List<RecipeAsset> Recipes = new List<RecipeAsset>();

        // ItemId -> 아이템. 처음 찾을 때 만듦.
        [NonSerialized]
        private Dictionary<ItemId, ItemAsset> lookup;

        /// <summary>
        /// 등록된 아이템. 없으면 null. 베이킹과 같은 규칙으로 빈 항목과 중복 Id 는 건너뜀.
        /// </summary>
        public ItemAsset Find(ItemId id)
        {
            if (lookup == null)
            {
                lookup = new Dictionary<ItemId, ItemAsset>();

                foreach (ItemAsset item in Items)
                {
                    if (item != null && item.Id != ItemId.None && item.Sprite != null)
                    {
                        lookup.TryAdd(item.Id, item);
                    }
                }
            }

            return lookup.TryGetValue(id, out ItemAsset found) ? found : null;
        }

        /// <summary>
        /// 결과물이 id 인 제작법. 없으면 null.
        /// </summary>
        public RecipeAsset FindRecipe(ItemId id)
        {
            foreach (RecipeAsset recipe in Recipes)
            {
                if (recipe != null && recipe.Result != null && recipe.Result.Id == id)
                {
                    return recipe;
                }
            }

            return null;
        }

        private void OnValidate()
        {
            lookup = null;
        }
    }
}
