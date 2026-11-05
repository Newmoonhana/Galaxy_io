using System;
using System.Collections.Generic;
using GalaxyIO.DOTS.Authoring;
using GalaxyIO.DOTS.Authoring.Config;
using GalaxyIO.DOTS.Client.Components.Singleton;
using GalaxyIO.DOTS.Client.Networking;
using GalaxyIO.DOTS.Components.Singleton;
using Newmoonhana.Runtime.Tools.PopupUI.Window;
using TMPro;
using Unity.Entities;
using Unity.NetCode;
using UnityEngine;
using UnityEngine.UI;

namespace GalaxyIO.Tools.PopupUI.Window
{
    /// <summary>
    /// 팀 모드 리스폰 창. 분류 버튼(계급장 | 우리 팀 기체 수 / 최대 수), 고른 분류의 기종 목록, 출격 버튼.
    /// 위에는 격추한 쪽, 아래에는 남은 대기 시간.
    /// </summary>
    public class WindowRespawnSelect : WindowRespawnBase
    {
        [SerializeField] private TextMeshProUGUI killer_tmp;
        [SerializeField] private TextMeshProUGUI countdown_tmp;

        [Tooltip("SpaceshipCategory 순서의 분류 버튼 목록. 템플릿은 SpaceshipCell.prefab")]
        [SerializeField] private WindowSlotList category_list;

        [Tooltip("고른 분류의 기종 목록. 템플릿은 SpaceshipCell.prefab")]
        [SerializeField] private WindowSlotList ship_list;

        [Tooltip("고른 기종으로 부활하는 버튼")]
        [SerializeField] private Button start_btn;

        [Tooltip("SpaceshipCategory 순서의 계급장")]
        [SerializeField] private Sprite[] badges;

        [Tooltip("고른 분류, 기종 버튼 색")]
        [SerializeField] private Color selectedTint = new Color(0.55F, 0.55F, 0.55F, 1.0F);

        /// <summary>
        /// 고른 기종을 다시 누름. 사양 창 열기 요청.
        /// </summary>
        public event Action<SpaceshipAsset> OnSpecRequested;

        /// <summary>
        /// 출격 가능 여부. 대기 시간 종료, 기종 선택, 분류 빈자리.
        /// </summary>
        public bool CanStart { get; private set; }

        private World clientWorld;
        private EntityQuery stateQuery;
        private EntityQuery registryQuery;

        // 고른 분류와 그 분류의 기종, 고른 기종
        private int category = -1;
        private int selected = -1;
        private readonly List<SpaceshipAsset> ships = new List<SpaceshipAsset>();

        // 분류 버튼에 마지막으로 쓴 글. 바뀔 때만 다시 씀.
        private readonly string[] categoryTexts = new string[4];

        private static readonly List<RecipeAsset.Ingredient> NoCost = new List<RecipeAsset.Ingredient>();

        protected override void Awake()
        {
            base.Awake();

            category_list.OnSelected += SelectCategory;
            ship_list.OnSelected += OnShipSelected;

            clientWorld = ClientServerBootstrap.ClientWorld;

            if (clientWorld == null) return;

            EntityManager entityManager = clientWorld.EntityManager;
            stateQuery = entityManager.CreateEntityQuery(ComponentType.ReadOnly<GameModeStateComponent>());
            registryQuery = entityManager.CreateEntityQuery(ComponentType.ReadOnly<SpaceshipRegistryAssetComponent>());
        }

        public override void Dispose()
        {
            base.Dispose();
            OnSpecRequested = null;
        }

        protected override void OnEnable()
        {
            base.OnEnable();
            start_btn.onClick.AddListener(RequestStart);
        }

        protected override void OnDisable()
        {
            base.OnDisable();
            start_btn.onClick.RemoveListener(RequestStart);
        }

        private void Update()
        {
            if (!ClientWorldScope.IsAlive(clientWorld) || stateQuery.IsEmpty) return;

            GameModeStateComponent state = stateQuery.GetSingleton<GameModeStateComponent>();
            float remaining = (float)(state.RespawnAt - clientWorld.Time.ElapsedTime);
            bool ready = remaining <= 0.0F;

            killer_tmp.text = state.Killer.IsEmpty ? string.Empty : string.Format(L10n.Get("Respawn/KilledBy"), state.Killer);
            countdown_tmp.text = ready
                ? L10n.Get("Respawn/Select")
                : string.Format(L10n.Get("Respawn/Countdown"), Mathf.CeilToInt(remaining));

            category_list.SetCount(categoryTexts.Length);

            for (int i = 0; i < categoryTexts.Length; i++)
            {
                string text = $"{state.Occupied[i]}/{state.Max[i]}";

                if (text == categoryTexts[i]) continue;

                categoryTexts[i] = text;
                category_list.SetView(i, new SlotView
                {
                    Icon      = i < badges.Length ? badges[i] : null,
                    Name      = text,
                    IsEnabled = state.Occupied[i] < state.Max[i],
                });
            }

            // 처음에는 빈자리가 있는 첫 분류를 보여 줌.
            if (category < 0)
            {
                for (int i = 0; i < categoryTexts.Length && category < 0; i++)
                {
                    if (state.Occupied[i] < state.Max[i]) SelectCategory(i);
                }
            }

            CanStart = ready && selected >= 0 && category >= 0 && state.Occupied[category] < state.Max[category];
            start_btn.interactable = CanStart;
        }

        /// <summary>
        /// 고른 기종으로 부활 요청. 서버가 거절하면 창이 다시 열림.
        /// </summary>
        public void RequestStart()
        {
            if (selected < 0 || selected >= ships.Count) return;
            if (!TryTakePlayerName(out string playerName)) return;

            ClientNetworkRequestManager.RequestRespawn(ships[selected].Id, playerName);
        }

        // 분류의 기종 목록을 채움. 프리팹이 없는 기종은 고를 수 없으므로 뺌.
        private void SelectCategory(int _category)
        {
            if (!ClientWorldScope.IsAlive(clientWorld) || registryQuery.IsEmpty) return;

            category = _category;
            ships.Clear();

            SpaceshipRegistryAsset registry = registryQuery.GetSingleton<SpaceshipRegistryAssetComponent>().Value;

            foreach (SpaceshipAsset spaceship in registry.Spaceships)
            {
                if (spaceship != null && spaceship.Prefab != null && (int)spaceship.Category == _category) ships.Add(spaceship);
            }

            ship_list.SetCount(ships.Count);

            for (int i = 0; i < ships.Count; i++)
            {
                if (!(ship_list.Element(i) is ShopItemElement cell)) continue;

                cell.SetItem(L10n.Get($"Spaceship/{ships[i].Id}"), ships[i].Sprite, NoCost);
                cell.SetOutline(ships[i].OutlineSensitivity);
            }

            for (int i = 0; i < category_list.Count; i++) Tint(category_list.Element(i), i == _category);

            SelectShip(-1);
        }

        // 고르지 않은 기종을 누르면 고르고, 고른 기종을 다시 누르면 사양 창 요청.
        private void OnShipSelected(int _index)
        {
            if (!IsActive() || _index >= ships.Count) return;

            if (_index != selected)
            {
                SelectShip(_index);
                return;
            }

            OnSpecRequested?.Invoke(ships[_index]);
        }

        private void SelectShip(int _index)
        {
            selected = _index;

            for (int i = 0; i < ships.Count; i++) Tint(ship_list.Element(i), i == _index);
        }

        private void Tint(SlotElement _element, bool _isSelected)
        {
            if (_element == null || _element.Btn.targetGraphic == null) return;

            _element.Btn.targetGraphic.color = _isSelected ? selectedTint : Color.white;
        }
    }
}
