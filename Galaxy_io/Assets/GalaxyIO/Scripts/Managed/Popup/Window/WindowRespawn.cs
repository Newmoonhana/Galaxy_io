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
using UnityEngine.Events;
using UnityEngine.UI;

namespace GalaxyIO.Tools.PopupUI.Window
{
    /// <summary>
    /// 리스폰 창. 자유 모드는 리스폰 버튼 하나, 팀 모드는 분류 버튼(계급장 | 우리 팀 기체 수 / 최대 수)과
    /// 고른 분류의 기종 목록을 보여 주고 기종을 누르면 그 기종으로 부활함. 위에는 격추한 쪽, 아래에는 남은 대기 시간.
    /// </summary>
    public class WindowRespawn : WindowBase
    {
        [SerializeField] private Button quit_btn;

        [Tooltip("자유 모드의 리스폰 버튼")]
        [SerializeField] private GameObject respawn_obj;

        [Header("Team Mode")]
        [Tooltip("팀 모드에서만 켜는 화면")]
        [SerializeField] private GameObject mode_obj;

        [SerializeField] private TextMeshProUGUI killer_tmp;
        [SerializeField] private TextMeshProUGUI countdown_tmp;

        [Tooltip("SpaceshipCategory 순서의 분류 버튼 목록. 템플릿은 SpaceshipCell.prefab")]
        [SerializeField] private WindowSlotList category_list;

        [Tooltip("고른 분류의 기종 목록. 템플릿은 SpaceshipCell.prefab")]
        [SerializeField] private WindowSlotList ship_list;

        [Tooltip("SpaceshipCategory 순서의 계급장")]
        [SerializeField] private Sprite[] badges;

        [Tooltip("팀 모드에서 나가기 버튼을 옮길 위치")]
        [SerializeField] private Vector2 modeQuitPosition = new Vector2(0.0F, -420.0F);

        private UnityEvent _quit;

        private World clientWorld;
        private EntityQuery modeQuery;
        private EntityQuery stateQuery;
        private EntityQuery registryQuery;

        // 고른 분류와 그 분류의 기종
        private int category = -1;
        private readonly List<SpaceshipAsset> ships = new List<SpaceshipAsset>();

        // 분류 버튼에 마지막으로 쓴 글. 바뀔 때만 다시 씀.
        private readonly string[] categoryTexts = new string[4];

        private static readonly List<RecipeAsset.Ingredient> NoCost = new List<RecipeAsset.Ingredient>();

        protected override void Awake()
        {
            base.Awake();
            _quit = new UnityEvent();

            category_list.OnSelected += SelectCategory;
            ship_list.OnSelected += OnShipSelected;

            clientWorld = ClientServerBootstrap.ClientWorld;

            if (clientWorld == null) return;

            EntityManager entityManager = clientWorld.EntityManager;
            modeQuery = entityManager.CreateEntityQuery(ComponentType.ReadOnly<GameModeComponent>());
            stateQuery = entityManager.CreateEntityQuery(ComponentType.ReadOnly<GameModeStateComponent>());
            registryQuery = entityManager.CreateEntityQuery(ComponentType.ReadOnly<SpaceshipRegistryAssetComponent>());
        }

        public override void Dispose()
        {
            base.Dispose();
            _quit?.RemoveAllListeners();
            _quit = null;
        }

        protected override void OnEnable()
        {
            base.OnEnable();
            quit_btn.onClick.AddListener(OnQuitBtnPress);
        }

        protected override void OnDisable()
        {
            base.OnDisable();
            quit_btn.onClick.RemoveListener(OnQuitBtnPress);
        }

        private void Update()
        {
            bool teamMode = ClientWorldScope.IsAlive(clientWorld) && !modeQuery.IsEmpty;

            respawn_obj.SetActive(!teamMode);
            mode_obj.SetActive(teamMode);

            if (!teamMode) return;

            ((RectTransform)quit_btn.transform).anchoredPosition = modeQuitPosition;

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

            bool room = category >= 0 && state.Occupied[category] < state.Max[category];

            for (int i = 0; i < ships.Count; i++)
            {
                SlotElement element = ship_list.Element(i);
                if (element != null) element.Btn.interactable = ready && room;
            }
        }

        public void AddListener(UnityAction _onQuit)
        {
            _quit.AddListener(_onQuit);
        }

        /// <summary>
        /// 자유 모드의 리스폰 버튼. 요청이 접수되면 태그가 지워져 창이 닫힘.
        /// </summary>
        public void OnPressRespawn()
        {
            ClientNetworkRequestManager.RequestRespawn();
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
        }

        // 고른 기종으로 부활 요청. 서버가 거절하면 창이 다시 열림.
        private void OnShipSelected(int _index)
        {
            if (!IsActive() || _index >= ships.Count) return;

            ClientNetworkRequestManager.RequestRespawn(ships[_index].Id);
        }

        private void OnQuitBtnPress()
        {
            if (!IsActive()) return;
            _quit?.Invoke();
        }
    }
}
