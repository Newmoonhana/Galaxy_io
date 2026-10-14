using System;
using System.Collections.Generic;
using GalaxyIO.DOTS.Authoring;
using GalaxyIO.DOTS.Authoring.Config;
using GalaxyIO.DOTS.Components.Singleton;
using GalaxyIO.DOTS.Inventory;
using Newmoonhana.Runtime.Tools.PopupUI.Window;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.Localization;
using UnityEngine.Localization.Settings;
using UnityEngine.UI;

namespace GalaxyIO.Tools.PopupUI.Window
{
    /// <summary>
    /// 인벤토리 메인 창. 우주선 / 포탑 상점 목록, 현재 우주선 배치도, 보유 재료를 가짐.
    /// 포탑 칸을 누르면 포탑 상점으로, 버튼이 아닌 곳을 누르면 우주선 상점으로 돌아감.
    /// </summary>
    public class WindowInventory : WindowBase, IPointerDownHandler
    {
        [Header("Spaceship Market")]
        [Tooltip("우주선 상점 화면. 포탑 상점과 번갈아 켜짐")]
        [SerializeField] private GameObject spaceshipMarket_obj;

        [Tooltip("SpaceshipCategory 순서의 분류별 칸 목록. 템플릿은 SpaceshipCell.prefab")]
        [SerializeField] private WindowSlotList[] category_lists;

        [Tooltip("SpaceshipCategory 순서의 분류 제목")]
        [SerializeField] private TextMeshProUGUI[] category_tmps;

        [Header("Turret Market")]
        [SerializeField] private GameObject turretMarket_obj;

        [Tooltip("포탑 줄 목록. 템플릿은 TurretRow.prefab")]
        [SerializeField] private WindowSlotList turret_list;

        [Header("Layout")]
        [Tooltip("현재 우주선 외곽선. 외곽선 머티리얼을 씀")]
        [SerializeField] private Image layout_img;

        [Tooltip("포탑 칸 목록. 칸을 누르면 그 칸의 포탑 상점을 엶. 외곽선 이미지 중심 기준으로 놓음")]
        [SerializeField] private WindowSlotList turretSlot_list;

        [Header("Wallet")]
        [Tooltip("보유 재료 줄 목록. 템플릿은 WalletRow.prefab")]
        [SerializeField] private WindowSlotList wallet_list;

        [Header("Buttons")]
        [SerializeField] private Button exit_btn;

        /// <summary>우주선 칸 누름.</summary>
        public event Action<SpaceshipAsset> OnSpaceshipPressed;

        /// <summary>포탑 줄 누름.</summary>
        public event Action<RecipeAsset> OnTurretPressed;

        private UnityEvent _exit;

        // 분류별로 칸에 보여준 기종. Build 에서 채움
        private readonly List<SpaceshipAsset>[] spaceships =
            new List<SpaceshipAsset>[Enum.GetValues(typeof(SpaceshipCategory)).Length];

        // 포탑 줄에 보여준 제작법
        private List<RecipeAsset> recipes;

        // 목록을 만들어 둠. 언어가 바뀌면 다시 만듦
        private bool isBuilt;

        // 외곽선 이미지의 원래 머티리얼. 감도별 사본의 원본
        private Material outlineMaterial;

        /// <summary>포탑 상점을 연 포탑 칸 번호. 0 이 주 포탑.</summary>
        public int SelectedSlot { get; private set; }

        protected override void Awake()
        {
            base.Awake();

            _exit = new UnityEvent();

            for (int i = 0; i < category_lists.Length; i++)
            {
                int category = i;
                spaceships[i] = new List<SpaceshipAsset>();
                category_lists[i].OnSelected += index => RelaySpaceship(category, index);
            }

            turret_list.OnSelected += RelayTurret;
            turretSlot_list.OnSelected += OnTurretSlotPress;
        }

        public override void Dispose()
        {
            base.Dispose();

            OnSpaceshipPressed = null;
            OnTurretPressed = null;
            _exit?.RemoveAllListeners();
            _exit = null;
        }

        protected override void OnEnable()
        {
            base.OnEnable();
            exit_btn.onClick.AddListener(OnExitBtnPress);
            LocalizationSettings.SelectedLocaleChanged += OnLocaleChanged;
        }

        protected override void OnDisable()
        {
            base.OnDisable();
            exit_btn.onClick.RemoveListener(OnExitBtnPress);
            LocalizationSettings.SelectedLocaleChanged -= OnLocaleChanged;
        }

        /// <summary>
        /// 생성 직후 호출. 닫기 처리는 생성한 쪽이 담당.
        /// </summary>
        public void AddListener(UnityAction _onExit) => _exit.AddListener(_onExit);

        /// <summary>
        /// 창이 열려 있는 동안 매 프레임 호출됨.
        /// </summary>
        /// <param name="_current">타고 있는 기종. 상점 기종이 아니면 null</param>
        /// <param name="_turret">주 포탑 칸에 장착한 포탑 아이콘</param>
        public void Refresh(
            PlayerInventory _inventory,
            ItemRegistryAsset _items,
            SpaceshipRegistryAsset _spaceships,
            SpaceshipAsset _current,
            Sprite _turret)
        {
            if (!isBuilt)
            {
                Build(_items, _spaceships);
                isBuilt = true;
            }

            for (int i = 0; i < category_lists.Length; i++)
                for (int j = 0; j < spaceships[i].Count; j++)
                    (category_lists[i].Element(j) as ShopItemElement)?.SetCosts(_inventory, false);

            for (int i = 0; i < recipes.Count; i++)
                (turret_list.Element(i) as ShopItemElement)?.SetCosts(_inventory, _inventory.IsUnlocked(recipes[i].Result.Id));

            for (int i = 0; i < PlayerInventory.Ingredients.Length; i++)
                (wallet_list.Element(i) as IngredientElement)?.SetAmount(_inventory.Count(PlayerInventory.Ingredients[i]), false);

            RefreshLayout(_current, _turret);
        }

        private void Build(ItemRegistryAsset _items, SpaceshipRegistryAsset _spaceships)
        {
            for (int i = 0; i < category_lists.Length; i++)
            {
                spaceships[i].Clear();

                foreach (SpaceshipAsset spaceship in _spaceships.Spaceships)
                    if (spaceship != null && (int)spaceship.Category == i) spaceships[i].Add(spaceship);

                category_lists[i].SetCount(spaceships[i].Count);

                if (i < category_tmps.Length) category_tmps[i].text = L10n.Get($"Category/{(SpaceshipCategory)i}");

                for (int j = 0; j < spaceships[i].Count; j++)
                {
                    SpaceshipAsset spaceship = spaceships[i][j];
                    var cell = category_lists[i].Element(j) as ShopItemElement;
                    if (cell == null) continue;

                    cell.SetItem(L10n.Get($"Spaceship/{spaceship.Id}"), spaceship.Sprite, spaceship.Ingredients);
                    cell.SetOutline(spaceship.OutlineSensitivity);
                }
            }

            recipes = _items.Recipes.FindAll(recipe => recipe != null && recipe.Result != null);
            turret_list.SetCount(recipes.Count);

            for (int i = 0; i < recipes.Count; i++)
            {
                ItemAsset turret = recipes[i].Result;
                (turret_list.Element(i) as ShopItemElement)?.SetItem(
                    L10n.Get($"Item/{turret.Id}"), turret.Sprite, recipes[i].Ingredients);
            }

            wallet_list.SetCount(PlayerInventory.Ingredients.Length);

            for (int i = 0; i < PlayerInventory.Ingredients.Length; i++)
            {
                ItemAsset item = _items.Find(PlayerInventory.Ingredients[i]);
                (wallet_list.Element(i) as IngredientElement)?.SetItem(
                    item != null ? item.Sprite : null, L10n.Get($"Item/{PlayerInventory.Ingredients[i]}"));
            }
        }

        // 외곽선과 포탑 칸. 0 번 칸만 주 포탑. 자동 포탑 칸은 서버가 아직 장착하지 않아 비워 둠
        private void RefreshLayout(SpaceshipAsset _current, Sprite _turret)
        {
            Sprite sprite = _current != null ? _current.Sprite : null;

            layout_img.sprite = sprite;
            layout_img.enabled = sprite != null;

            int slots = sprite != null ? _current.TurretSlots.Count : 0;
            turretSlot_list.SetCount(slots);

            if (sprite == null) return;

            if (outlineMaterial == null) outlineMaterial = layout_img.material;
            layout_img.material = OutlineMaterials.Get(outlineMaterial, _current.OutlineSensitivity);

            for (int i = 0; i < slots; i++)
            {
                SlotElement slot = turretSlot_list.Element(i);
                ((RectTransform)slot.transform).anchoredPosition = _current.TurretSlotIn(i, layout_img.rectTransform.rect.size);

                // 빈 칸도 누를 수 있어야 하므로 IsEmpty 는 쓰지 않음
                slot.SetView(new SlotView { Icon = i == 0 ? _turret : null, IsEnabled = true });
            }
        }

        private void ShowTurretMarket(bool _isShown)
        {
            spaceshipMarket_obj.SetActive(!_isShown);
            turretMarket_obj.SetActive(_isShown);
        }

        private void OnLocaleChanged(Locale _locale) => isBuilt = false;

        private void RelaySpaceship(int _category, int _index)
        {
            if (!IsActive() || !IsOpen()) return;

            OnSpaceshipPressed?.Invoke(spaceships[_category][_index]);
        }

        private void RelayTurret(int _index)
        {
            if (!IsActive() || !IsOpen()) return;

            OnTurretPressed?.Invoke(recipes[_index]);
        }

        private void OnTurretSlotPress(int _slot)
        {
            if (!IsActive()) return;

            SelectedSlot = _slot;
            ShowTurretMarket(true);
        }

        private void OnExitBtnPress()
        {
            if (!IsActive()) return;
            _exit?.Invoke();
        }

        // 버튼이 아닌 곳을 누름. 우주선 상점으로 돌아감
        public void OnPointerDown(PointerEventData _eventData)
        {
            if (!IsActive() || !IsOpen()) return;

            ShowTurretMarket(false);
        }
    }
}
