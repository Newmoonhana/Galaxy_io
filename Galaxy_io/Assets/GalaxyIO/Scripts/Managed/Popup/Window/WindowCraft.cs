using System;
using System.Collections.Generic;
using GalaxyIO.DOTS.Authoring;
using GalaxyIO.DOTS.Authoring.Config;
using GalaxyIO.DOTS.Components.Singleton;
using GalaxyIO.DOTS.Inventory;
using GalaxyIO.Tools;
using Newmoonhana.Runtime.Tools.PopupUI.Window;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace GalaxyIO.Tools.PopupUI.Window
{
    /// <summary>
    /// 설계도 한 장의 제작 창. 칸 창 위에 열리고 패널 밖을 누르면 닫힘.
    /// </summary>
    public class WindowCraft : WindowBase, IPointerDownHandler
    {
        [Header("Craft")]
        [Tooltip("이 영역 밖을 누르면 창을 닫음")]
        [SerializeField] private RectTransform panel_rt;

        [Tooltip("결과물 이름")]
        [SerializeField] private TextMeshProUGUI name_tmp;

        [Tooltip("결과물 아이콘")]
        [SerializeField] private Image result_img;

        [Tooltip("재료 줄 목록. 템플릿은 RecipeRow.prefab")]
        [SerializeField] private WindowSlotList ingredient_list;

        [SerializeField] private Button craft_btn;

        /// <summary>제작 버튼. 인자는 설계도가 든 칸 번호.</summary>
        public event Action<int> OnCraft;

        /// <summary>패널 밖 누름, 또는 설계도가 사라져 닫아야 함.</summary>
        public event Action OnClosed;

        // 설계도가 든 칸 번호
        private int slot = -1;

        // 이름 / 아이콘 / 재료 줄을 채워 둔 설계도. 바뀔 때만 다시 채움
        private ItemId shownBlueprint = ItemId.None;

        public override void Dispose()
        {
            base.Dispose();

            OnCraft = null;
            OnClosed = null;
        }

        protected override void OnEnable()
        {
            base.OnEnable();
            craft_btn.onClick.AddListener(OnCraftBtnPress);
        }

        protected override void OnDisable()
        {
            base.OnDisable();
            craft_btn.onClick.RemoveListener(OnCraftBtnPress);
        }

        /// <summary>
        /// 열 때 호출. 설계도가 든 칸을 정함.
        /// </summary>
        public void SetSlot(int _slot)
        {
            slot = _slot;
            shownBlueprint = ItemId.None;
        }

        /// <summary>
        /// 창이 열려 있는 동안 매 프레임 호출됨.
        /// </summary>
        public void Refresh(PlayerInventory _inventory, ItemRegistryAsset _registry)
        {
            ItemId blueprintId = slot >= 0 && slot < _inventory.Length ? _inventory[slot].Id : ItemId.None;

            // 설계도를 다 썼거나 다른 칸으로 옮겼으면 닫음
            if (!(_registry.Find(blueprintId) is BlueprintItemAsset blueprint)
                || blueprint.Recipe == null
                || blueprint.Recipe.Result == null)
            {
                OnClosed?.Invoke();
                return;
            }

            List<RecipeAsset.Ingredient> ingredients = blueprint.Recipe.Ingredients;

            if (shownBlueprint != blueprintId)
            {
                shownBlueprint = blueprintId;
                name_tmp.text = L10n.Get($"Item/{blueprint.Recipe.Result.Id}");
                result_img.sprite = blueprint.Recipe.Result.Sprite;

                ingredient_list.SetCount(ingredients.Count);

                for (int i = 0; i < ingredients.Count; i++)
                {
                    ItemAsset item = ingredients[i].Item;

                    // 비어 있는 재료는 제작 조건에서도 빠지므로 빈 칸으로 둠
                    Element(i)?.SetItem(item != null ? item.Sprite : null,
                        item != null ? L10n.Get($"Item/{item.Id}") : string.Empty);
                }
            }

            for (int i = 0; i < ingredients.Count; i++)
            {
                if (ingredients[i].Item == null) continue;

                Element(i)?.SetCount(_inventory.Count(ingredients[i].Item.Id), ingredients[i].Count);
            }

            craft_btn.interactable = _inventory.HasIngredients(blueprint.Recipe);
        }

        private IngredientElement Element(int _index) => ingredient_list.Element(_index) as IngredientElement;

        private void OnCraftBtnPress()
        {
            if (!IsActive()) return;
            OnCraft?.Invoke(slot);
        }

        // 버튼이 아닌 곳을 누름. 패널 밖이면 닫음
        public void OnPointerDown(PointerEventData _eventData)
        {
            if (!IsActive() || !IsOpen()) return;
            if (RectTransformUtility.RectangleContainsScreenPoint(panel_rt, _eventData.position, _eventData.pressEventCamera))
                return;

            OnClosed?.Invoke();
        }
    }
}
