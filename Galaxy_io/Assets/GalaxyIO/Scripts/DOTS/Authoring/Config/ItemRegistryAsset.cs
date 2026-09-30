using System.Collections.Generic;
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
    }
}
