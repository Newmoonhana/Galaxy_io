using System;
using System.Collections.Generic;
using UnityEngine;

namespace GalaxyIO.DOTS.Authoring
{
    /// <summary>
    /// 포탑 제작법. 재료를 소모해 결과물 포탑을 사서 장착함.
    /// <see cref="Config.ItemRegistryAsset.Recipes"/>에 등록하면 포탑 상점에 나타남.
    /// </summary>
    [CreateAssetMenu(fileName = "SO_Recipe", menuName = "GalaxyIO/Recipe")]
    public sealed class RecipeAsset : ScriptableObject
    {
        [Serializable]
        public struct Ingredient
        {
            public ItemAsset Item;

            [Min(1)]
            public int Count;
        }

        [SerializeField]
        public List<Ingredient> Ingredients = new List<Ingredient>();

        [SerializeField]
        [Tooltip("제작 결과물")]
        public ItemAsset Result;
    }
}
