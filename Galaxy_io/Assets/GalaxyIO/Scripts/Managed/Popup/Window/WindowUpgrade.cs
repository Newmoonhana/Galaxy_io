using System;
using System.Collections.Generic;
using GalaxyIO.DOTS.Components.Actor;
using Newmoonhana.Runtime.Tools.PopupUI.Window;
using Unity.Entities;
using UnityEngine;
using UnityEngine.Localization;
using UnityEngine.Localization.Settings;

namespace GalaxyIO.Tools.PopupUI.Window
{
    /// <summary>
    /// 우주선 강화 창. 인벤토리 창 옆에 능력치 줄을 세로로 늘어놓음. 최대 단계가 0 인 능력치는 뺌.
    /// </summary>
    public class WindowUpgrade : WindowBase
    {
        [Header("Upgrade")]
        [Tooltip("능력치 줄 목록. 템플릿은 UpgradeRow.prefab")]
        [SerializeField] private WindowSlotList upgrade_list;

        [Tooltip("위에서부터 줄 색")]
        [SerializeField] private Color[] colors;

        /// <summary>줄 누름. 인자는 강화할 능력치.</summary>
        public event Action<UpgradeStat> OnUpgrade;

        // UpgradeStat 순서의 능력치 이름. 언어가 바뀌면 다시 채움
        private readonly string[] names = new string[Upgrade.Count];

        // 줄마다 보여주는 능력치
        private readonly List<UpgradeStat> rows = new List<UpgradeStat>();

        // 줄을 만든 능력치 집합(UpgradeStat 비트). 바뀌면 줄을 다시 만듦
        private int shownStats = -1;

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
        /// <param name="_powerCells">가진 파워 셀 수</param>
        /// <param name="_powerCell">파워 셀 아이콘</param>
        public void Refresh(
            DynamicBuffer<UpgradeElement> _upgrades,
            DynamicBuffer<UpgradeLevelElement> _levels,
            int _powerCells,
            Sprite _powerCell)
        {
            rows.Clear();
            int stats = 0;

            for (int i = 0; i < _upgrades.Length; i++)
            {
                if (_upgrades[i].MaxLevel == 0) continue;

                rows.Add((UpgradeStat)i);
                stats |= 1 << i;
            }

            if (stats != shownStats)
            {
                upgrade_list.SetCount(rows.Count);
                shownStats = stats;

                for (int i = 0; i < rows.Count && i < colors.Length; i++)
                    Element(i)?.SetColor(colors[i]);
            }

            for (int i = 0; i < rows.Count; i++)
            {
                int stat = (int)rows[i];
                int level = _levels[stat].Value;
                bool isMax = level >= _upgrades[stat].MaxLevel;

                // 단계는 1 부터 표기함. 다음 강화에 드는 파워 셀 수와 같음
                Element(i)?.SetUpgrade(
                    names[stat] ??= L10n.Get($"Upgrade/{rows[i]}"),
                    _powerCell,
                    Upgrade.Cost(level),
                    isMax,
                    !isMax && _powerCells >= Upgrade.Cost(level));
            }
        }

        private UpgradeRowElement Element(int _index) => upgrade_list.Element(_index) as UpgradeRowElement;

        private void OnLocaleChanged(Locale _locale) => Array.Clear(names, 0, names.Length);

        private void RelayUpgrade(int _index)
        {
            if (!IsOpen() || _index >= rows.Count) return;

            OnUpgrade?.Invoke(rows[_index]);
        }
    }
}
