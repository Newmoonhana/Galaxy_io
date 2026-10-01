using UnityEngine;

namespace GalaxyIO.DOTS.Authoring
{
    /// <summary>
    /// 설계도 아이템. 인벤토리에서 <see cref="Recipe"/>대로 결과물을 제작할 수 있음.
    /// </summary>
    [CreateAssetMenu(fileName = "SO_Item_Blueprint", menuName = "GalaxyIO/Blueprint Item")]
    public sealed class BlueprintItemAsset : ItemAsset
    {
        [SerializeField]
        [Tooltip("이 설계도로 만드는 제작법")]
        public RecipeAsset Recipe;
    }
}
