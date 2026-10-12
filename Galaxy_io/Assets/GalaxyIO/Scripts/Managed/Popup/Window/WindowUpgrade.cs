using System;
using GalaxyIO.DOTS.Components.Actor;
using Newmoonhana.Runtime.Tools.PopupUI.Window;
using Unity.Entities;
using UnityEngine;
using UnityEngine.Localization;
using UnityEngine.Localization.Settings;

namespace GalaxyIO.Tools.PopupUI.Window
{
    /// <summary>
    /// 우주선 강화 창. 칸 창 옆에 능력치 줄을 세로로 늘어놓음.
    /// </summary>
    public class WindowUpgrade : WindowBase
    {
        [Header("Upgrade")]
        [Tooltip("능력치 줄 목록. 템플릿은 UpgradeRow.prefab")]
        [SerializeField] private WindowSlotList upgrade_list;

        [Tooltip("UpgradeStat 순서의 줄 색")]
        [SerializeField] private Color[] colors;

        /// <summary>줄 누름. 인자는 강화할 능력치.</summary>
        public event Action<UpgradeStat> OnUpgrade;

        // UpgradeStat 순서의 능력치 이름. 언어가 바뀌면 다시 채움
        private readonly string[] names = new string[Upgrade.Count];

        // 줄 색까지 입혀 둔 줄 수
        private int builtCount;

        protected override void Awake()
        {
            base.Awake();

            upgrade_list.OnSelected += RelayUpgrade;
        }

        public override void Dispose()
        {
            base.Dispose();

            OnUpgrade = null;
        }

        protected override void OnEnable()
        {
            base.OnEnable();
            LocalizationSettings.SelectedLocaleChanged += OnLocaleChanged;
        }

        protected override void OnDisable()
        {
            base.OnDisable();
            LocalizationSettings.SelectedLocaleChanged -= OnLocaleChanged;
        }

        /// <summary>
        /// 창이 열려 있는 동안 매 프레임 호출됨.
        /// </summary>
        /// <param name="_modules">가진 강화 모듈 수</param>
        /// <param name="_module">강화 모듈 아이콘</param>
        public void Refresh(
            DynamicBuffer<UpgradeElement> _upgrades,
            DynamicBuffer<UpgradeLevelElement> _levels,
            int _modules,
            Sprite _module)
        {
            if (builtCount != _upgrades.Length)
            {
                upgrade_list.SetCount(_upgrades.Length);
                builtCount = _upgrades.Length;

                for (int i = 0; i < builtCount && i < colors.Length; i++)
                    Element(i)?.SetColor(colors[i]);
            }

            for (int i = 0; i < builtCount; i++)
            {
                int level = _levels[i].Value;
                bool isMax = level >= _upgrades[i].MaxLevel;

                // 단계는 1 부터 표기함. 다음 강화에 드는 모듈 수와 같음
                Element(i)?.SetUpgrade(
                    names[i] ??= L10n.Get($"Upgrade/{(UpgradeStat)i}"),
                    _module,
                    Upgrade.Cost(level),
                    isMax,
                    !isMax && _modules >= Upgrade.Cost(level));
            }
        }

        private UpgradeRowElement Element(int _index) => upgrade_list.Element(_index) as UpgradeRowElement;

        private void OnLocaleChanged(Locale _locale) => Array.Clear(names, 0, names.Length);

        private void RelayUpgrade(int _index)
        {
            if (!IsOpen()) return;

            OnUpgrade?.Invoke((UpgradeStat)_index);
        }
    }
}
