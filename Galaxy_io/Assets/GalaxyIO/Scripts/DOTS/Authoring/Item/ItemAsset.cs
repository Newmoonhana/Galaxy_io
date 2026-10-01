using GalaxyIO.DOTS.Components.Singleton;
using UnityEngine;

namespace GalaxyIO.DOTS.Authoring
{
    /// <summary>
    /// 아이템 정의. <see cref="Config.ItemRegistryAsset"/>에 등록해야 게임에 나타남.
    /// </summary>
    [CreateAssetMenu(fileName = "SO_Item", menuName = "GalaxyIO/Item")]
    public class ItemAsset : ScriptableObject
    {
        [SerializeField]
        [Tooltip("코드와 네트워크가 이 아이템을 가리키는 키")]
        public ItemId Id;

        [SerializeField]
        [Tooltip("월드에 떨어졌을 때의 스프라이트. 줍기 충돌체도 이 크기를 따름.")]
        public Sprite Sprite;

        [SerializeField]
        [Tooltip("들어갈 수 있는 인벤토리 칸을 정함.")]
        public ItemType Type = ItemType.Material;

        [SerializeField]
        [Tooltip("인벤토리 한 칸에 쌓을 수 있는 최대 개수.")]
        [Range(1, 255)]
        public int Stacking = 255;
    }
}