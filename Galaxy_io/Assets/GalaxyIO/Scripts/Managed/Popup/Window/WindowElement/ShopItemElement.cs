using System.Collections.Generic;
using GalaxyIO.DOTS.Authoring;
using GalaxyIO.DOTS.Inventory;
using Newmoonhana.Runtime.Tools.PopupUI.Window;
using UnityEngine;

namespace GalaxyIO.Tools.PopupUI.Window
{
    /// <summary>
    /// 상점의 우주선 칸, 포탑 줄. 이름, 아이콘, 재료 가격표.
    /// 잠금 해제한 상품은 가격표 대신 잠금 해제 표시(size_tmp)를 띄움.
    /// </summary>
    public sealed class ShopItemElement : SlotElement
    {
        [Tooltip("재료 가격표 목록. 템플릿은 CostTag.prefab")]
        [SerializeField] private WindowSlotList cost_list;

        private List<RecipeAsset.Ingredient> ingredients;

        // 아이콘의 원래 외곽선 머티리얼. 감도별 사본의 원본
        private Material iconMaterial;

        /// <summary>
        /// 표시할 상품. 목록을 만들 때만 호출함.
        /// </summary>
        public void SetItem(string _name, Sprite _icon, List<RecipeAsset.Ingredient> _ingredients)
        {
            ingredients = _ingredients;

            SetView(new SlotView
            {
                Icon      = _icon,
                Name      = _name,
                IsEnabled = true,
            });

            cost_list.SetCount(_ingredients.Count);

            for (int i = 0; i < _ingredients.Count; i++)
            {
                ItemAsset item = _ingredients[i].Item;

                // 비어 있는 재료는 구매 조건에서도 빠지므로 빈 칸으로 둠
                Element(i)?.SetItem(item != null ? item.Sprite : null, string.Empty);
            }
        }

        /// <summary>
        /// 아이콘 외곽선 감도. 목록을 만들 때만 호출함.
        /// </summary>
        public void SetOutline(float _sensitivity)
        {
            if (iconMaterial == null) iconMaterial = icon_img.material;

            icon_img.material = OutlineMaterials.Get(iconMaterial, _sensitivity);
        }

        /// <summary>
        /// 보유 / 필요 개수. 매 프레임 호출됨.
        /// </summary>
        public void SetCosts(PlayerInventory _inventory, bool _isUnlocked)
        {
            if (ingredients == null) return;

            cost_list.gameObject.SetActive(!_isUnlocked);

            if (size_tmp != null && size_tmp.enabled != _isUnlocked)
            {
                size_tmp.text = L10n.Get("Shop/Unlocked");
                size_tmp.enabled = _isUnlocked;
            }

            if (_isUnlocked) return;

            for (int i = 0; i < ingredients.Count; i++)
            {
                if (ingredients[i].Item == null) continue;

                Element(i)?.SetCount(_inventory.Count(ingredients[i].Item.Id), ingredients[i].Count);
            }
        }

        private IngredientElement Element(int _index) => cost_list.Element(_index) as IngredientElement;
    }
}
