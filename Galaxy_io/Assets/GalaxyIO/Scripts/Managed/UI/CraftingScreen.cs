using System.Collections.Generic;
using GalaxyIO.DOTS.Components.Actor;
using GalaxyIO.DOTS.Components.Singleton;
using GalaxyIO.Tools;
using GalaxyIO.Tools.PopupUI.Popup;
using TMPro;
using Unity.Entities;
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
        public void Refresh(
            DynamicBuffer<InventorySlotElement> slots,
            DynamicBuffer<ItemRegistryElement> registry,
            DynamicBuffer<RecipeIngredientElement> ingredients)
        {
            ItemId blueprintId = slot < slots.Length ? slots[slot].Id : ItemId.None;

            // 설계도를 다 썼거나 다른 칸으로 옮겼으면 닫음.
            if (!registry.Contains(blueprintId) || !registry.Contains(registry[(int)blueprintId].CraftResult))
            {
                gameObject.SetActive(false);
                return;
            }

            ItemRegistryElement blueprint = registry[(int)blueprintId];

            if (shownBlueprint != blueprintId)
            {
                shownBlueprint = blueprintId;
                nameLabel.text = L10n.Get($"Item/{blueprint.CraftResult}");
                resultIcon.sprite = registry[(int)blueprint.CraftResult].Sprite.Value;

                for (int i = 0; i < blueprint.IngredientCount; i++)
                {
                    if (i >= rows.Count)
                    {
                        rows.Add(Instantiate(rowPrefab, container));
                    }

                    ItemId id = ingredients[blueprint.IngredientStart + i].Id;
                    Sprite sprite = registry.Contains(id) ? registry[(int)id].Sprite.Value : null;

                    rows[i].gameObject.SetActive(true);
                    rows[i].SetItem(sprite, L10n.Get($"Item/{id}"));
                }

                for (int i = blueprint.IngredientCount; i < rows.Count; i++)
                {
                    rows[i].gameObject.SetActive(false);
                }
            }

            bool craftable = true;

            for (int i = 0; i < blueprint.IngredientCount; i++)
            {
                RecipeIngredientElement ingredient = ingredients[blueprint.IngredientStart + i];
                int have = Inventory.Count(slots, ingredient.Id);

                rows[i].SetCount(have, ingredient.Count);
                craftable &= have >= ingredient.Count;
            }

            craftButton.interactable = craftable;
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
