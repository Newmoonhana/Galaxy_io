using System.Collections.Generic;
using GalaxyIO.DOTS.Authoring;
using GalaxyIO.DOTS.Authoring.Config;
using GalaxyIO.DOTS.Components.Singleton;
using GalaxyIO.DOTS.Inventory;
using GalaxyIO.Tools;
using GalaxyIO.Tools.PopupUI.Popup;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace GalaxyIO.Scripts.Managed.UI
{
    /// <summary>
    /// 설계도 한 장의 제작 화면. <see cref="InventoryScreen"/> 위에 열리고 패널 밖을 누르면 닫힘.
    /// </summary>
    public sealed class CraftingScreen : MonoBehaviour, IPointerDownHandler
    {
        [SerializeField]
        [Tooltip("이 영역 밖을 누르면 닫음")]
        private RectTransform panel;

        [SerializeField]
        [Tooltip("결과물 이름")]
        private TMP_Text nameLabel;

        [SerializeField]
        [Tooltip("결과물 아이콘")]
        private Image resultIcon;

        [SerializeField]
        private RecipeRow rowPrefab;

        [SerializeField]
        [Tooltip("재료 줄이 쌓이는 부모. 위에서 아래로 정렬됨.")]
        private RectTransform container;

        [SerializeField]
        private Button craftButton;

        private readonly List<RecipeRow> rows = new List<RecipeRow>();

        private InventoryScreen screen;

        // 설계도가 든 칸 번호
        private int slot = -1;

        // 이름과 아이콘을 채워 둔 설계도. 바뀔 때만 다시 채움.
        private ItemId shownBlueprint = ItemId.None;

        public void Bind(InventoryScreen owner)
        {
            screen = owner;
            craftButton.onClick.AddListener(() => screen.OnCraftClick(slot));
        }

        public void Open(int blueprintSlot)
        {
            slot = blueprintSlot;
            shownBlueprint = ItemId.None;
            gameObject.SetActive(true);
        }

        /// <summary>
        /// 화면이 열려 있는 동안 <see cref="InventoryScreen"/>이 매 프레임 호출함.
        /// </summary>
        public void Refresh(PlayerInventory inventory, ItemRegistryAsset registry)
        {
            ItemId blueprintId = slot < inventory.Length ? inventory[slot].Id : ItemId.None;

            // 설계도를 다 썼거나 다른 칸으로 옮겼으면 닫음.
            if (!(registry.Find(blueprintId) is BlueprintItemAsset blueprint)
                || blueprint.Recipe == null
                || blueprint.Recipe.Result == null)
            {
                gameObject.SetActive(false);
                return;
            }

            List<RecipeAsset.Ingredient> ingredients = blueprint.Recipe.Ingredients;

            if (shownBlueprint != blueprintId)
            {
                shownBlueprint = blueprintId;
                nameLabel.text = L10n.Get($"Item/{blueprint.Recipe.Result.Id}");
                resultIcon.sprite = blueprint.Recipe.Result.Sprite;

                for (int i = 0; i < ingredients.Count; i++)
                {
                    if (i >= rows.Count)
                    {
                        rows.Add(Instantiate(rowPrefab, container));
                    }

                    ItemAsset item = ingredients[i].Item;

                    // 비어 있는 재료는 제작 조건에서도 빠지므로 숨김.
                    rows[i].gameObject.SetActive(item != null);

                    if (item != null)
                    {
                        rows[i].SetItem(item.Sprite, L10n.Get($"Item/{item.Id}"));
                    }
                }

                for (int i = ingredients.Count; i < rows.Count; i++)
                {
                    rows[i].gameObject.SetActive(false);
                }
            }

            for (int i = 0; i < ingredients.Count; i++)
            {
                if (ingredients[i].Item != null)
                {
                    rows[i].SetCount(inventory.Count(ingredients[i].Item.Id), ingredients[i].Count);
                }
            }

            craftButton.interactable = inventory.HasIngredients(blueprint.Recipe);
        }

        // 버튼이 아닌 곳을 누름. 패널 밖이면 닫음.
        public void OnPointerDown(PointerEventData eventData)
        {
            if (!RectTransformUtility.RectangleContainsScreenPoint(panel, eventData.position, eventData.pressEventCamera))
            {
                gameObject.SetActive(false);
            }
        }
    }
}
