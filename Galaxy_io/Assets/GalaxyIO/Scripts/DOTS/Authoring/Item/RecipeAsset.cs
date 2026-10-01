using System;
using System.Collections.Generic;
using UnityEngine;

namespace GalaxyIO.DOTS.Authoring
{
    /// <summary>
    /// 제작법. 재료를 소모해 결과물 한 개를 만듦. <see cref="BlueprintItemAsset"/>이 참조함.
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
