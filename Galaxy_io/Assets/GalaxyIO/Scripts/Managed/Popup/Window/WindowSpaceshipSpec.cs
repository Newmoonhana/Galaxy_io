using System.Collections.Generic;
using System.Text;
using GalaxyIO.DOTS.Authoring;
using GalaxyIO.DOTS.Components.Actor;
using GalaxyIO.DOTS.Components.Singleton;
using GalaxyIO.DOTS.Inventory;
using Newmoonhana.Runtime.Tools.PopupUI.Window;
using UnityEngine;
using UnityEngine.UI;

namespace GalaxyIO.Tools.PopupUI.Window
{
    /// <summary>
    /// 우주선 상세 사양 창. 외곽선, 포탑 칸, 필요 재료, 이름, 능력치, 장착 버튼.
    /// 능력치는 "기본값(+강화 한 단계당 증가량), 최대 단계"로 프리팹과 강화 에셋에서 읽음.
    /// </summary>
    public class WindowSpaceshipSpec : WindowSpecBase
    {
        [Header("Spaceship")]
        [Tooltip("우주선 외곽선. 외곽선 머티리얼을 씀")]
        [SerializeField] private Image outline_img;

        [Tooltip("포탑 칸 표시 목록. 외곽선 이미지 중심 기준으로 놓음")]
        [SerializeField] private WindowSlotList turretSlot_list;

        [Tooltip("재료 가격표 목록. 템플릿은 CostTag.prefab")]
        [SerializeField] private WindowSlotList cost_list;

        private SpaceshipAsset spaceship;

        // 외곽선 이미지의 원래 머티리얼. 감도별 사본의 원본
        private Material outlineMaterial;

        public SpaceshipId Spaceship => spaceship != null ? spaceship.Id : SpaceshipId.None;

        /// <summary>
        /// 열 때 호출. 보여줄 기종을 정함.
        /// </summary>
        public void SetSpaceship(SpaceshipAsset _spaceship)
        {
            spaceship = _spaceship;

            name_tmp.text = L10n.Get($"Spaceship/{_spaceship.Id}");

            if (outlineMaterial == null) outlineMaterial = outline_img.material;

            outline_img.sprite = _spaceship.Sprite;
            outline_img.enabled = _spaceship.Sprite != null;
            outline_img.material = OutlineMaterials.Get(outlineMaterial, _spaceship.OutlineSensitivity);

            int slots = _spaceship.Sprite != null ? _spaceship.TurretSlots.Count : 0;
            turretSlot_list.SetCount(slots);

            for (int i = 0; i < slots; i++)
                ((RectTransform)turretSlot_list.Element(i).transform).anchoredPosition =
                    _spaceship.TurretSlotIn(i, outline_img.rectTransform.rect.size);

            List<RecipeAsset.Ingredient> ingredients = _spaceship.Ingredients;
            cost_list.SetCount(ingredients.Count);

            for (int i = 0; i < ingredients.Count; i++)
            {
                ItemAsset item = ingredients[i].Item;
                Element(i)?.SetItem(item != null ? item.Sprite : null, string.Empty);
            }

            // 프리팹이 없는 기종은 능력치를 알 수 없음
            if (_spaceship.Prefab == null)
            {
                spec_tmp.text = L10n.Get("Shop/NoSpec");
                return;
            }

            SpaceshipAuthoring ship = _spaceship.Prefab;
            SpaceshipUpgradeAsset upgrade = ship.Upgrade;
            var sb = new StringBuilder();

            string maxLevel = L10n.Get("Spec/MaxLevel");

            // "이름: 기본값(+단계당 증가량), 최대 Lv. n". 강화 사양이 없으면 기본값만 씀
            void Stat(UpgradeStat _stat, float _base, float _amount, int _maxLevel, string _unit = "")
            {
                sb.Append($"{L10n.Get($"Upgrade/{_stat}")}: {_base:0.###}{_unit}");
                if (upgrade != null) sb.Append($"(+{_amount:0.###}{_unit}), {string.Format(maxLevel, _maxLevel)}");
                sb.AppendLine();
            }

            sb.AppendLine(L10n.Get("Spec/Offensive"));
            Stat(UpgradeStat.Damage, 0.0F, upgrade != null ? upgrade.Damage.Damage * 100.0F : 0.0F, upgrade != null ? upgrade.Damage.MaxLevel : 0, "%");
            Stat(UpgradeStat.MaxHeat, ship.MaxHeat, upgrade != null ? upgrade.MaxHeat.MaxHeat : 0.0F, upgrade != null ? upgrade.MaxHeat.MaxLevel : 0);
            sb.AppendLine();

            sb.AppendLine(L10n.Get("Spec/Durability"));
            Stat(UpgradeStat.MaxHealth, ship.MaxHealth, upgrade != null ? upgrade.MaxHealth.MaxHealth : 0.0F, upgrade != null ? upgrade.MaxHealth.MaxLevel : 0);
            Stat(UpgradeStat.Repair, ship.Repair, upgrade != null ? upgrade.Repair.Repair : 0.0F, upgrade != null ? upgrade.Repair.MaxLevel : 0);
            sb.AppendLine();

            sb.AppendLine(L10n.Get("Spec/Movement"));
            Stat(UpgradeStat.Speed, ship.Speed, upgrade != null ? upgrade.Speed.MaxSpeed : 0.0F, upgrade != null ? upgrade.Speed.MaxLevel : 0);
            Stat(UpgradeStat.Maneuver, ship.Maneuver, upgrade != null ? upgrade.Maneuver.Maneuver : 0.0F, upgrade != null ? upgrade.Maneuver.MaxLevel : 0);
            sb.AppendLine();

            sb.AppendLine(L10n.Get("Spec/Radar"));
            Stat(UpgradeStat.RadarRange, ship.RadarRange, upgrade != null ? upgrade.RadarRange.Range : 0.0F, upgrade != null ? upgrade.RadarRange.MaxLevel : 0);
            Stat(UpgradeStat.RadarSweep, ship.RadarSweepSpeed, upgrade != null ? upgrade.RadarSweep.SweepSpeed : 0.0F, upgrade != null ? upgrade.RadarSweep.MaxLevel : 0);

            spec_tmp.text = sb.ToString().TrimEnd();
        }

        /// <summary>
        /// 창이 열려 있는 동안 매 프레임 호출됨.
        /// </summary>
        /// <param name="_current">타고 있는 기종</param>
        public void Refresh(PlayerInventory _inventory, SpaceshipId _current)
        {
            List<RecipeAsset.Ingredient> ingredients = spaceship.Ingredients;

            for (int i = 0; i < ingredients.Count; i++)
            {
                if (ingredients[i].Item == null) continue;

                Element(i)?.SetCount(_inventory.Count(ingredients[i].Item.Id), ingredients[i].Count);
            }

            if (_current == spaceship.Id) SetEquip("Shop/Equipped", false);
            else SetEquip("Shop/Equip", spaceship.Prefab != null && _inventory.HasIngredients(ingredients));
        }

        private IngredientElement Element(int _index) => cost_list.Element(_index) as IngredientElement;
    }
}
