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
    /// 능력치는 "기본값(+강화 한 단계당 증가량), 최대 단계"로 프리팹과 강화 에셋에서 읽음. 지원하지 않는 강화는 빼고, 강화 사양이 없는 기종은 능력치를 띄우지 않음.
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

            SpaceshipSpecAsset ship = _spaceship.Prefab.Spec;
            SpaceshipUpgradeAsset upgrade = ship != null ? ship.Upgrade : null;

            // 강화 사양이 없는 기종은 능력치 목록을 띄우지 않음
            if (upgrade == null)
            {
                spec_tmp.text = string.Empty;
                return;
            }

            var sb = new StringBuilder();

            string maxLevel = L10n.Get("Spec/MaxLevel");

            // 분류 제목. 그 분류의 첫 줄을 쓸 때 붙임. 지원하는 강화가 없는 분류는 통째로 빠짐
            string section = null;

            // "이름: 기본값(+단계당 증가량), 최대 Lv. n". 지원하지 않는 강화는 뺌
            void Stat(UpgradeStat _stat, float _base, float _amount, string _unit = "")
            {
                SpaceshipUpgradeAsset.Entry entry = upgrade.Get(_stat);

                if (!entry.Supported) return;

                if (section != null)
                {
                    if (sb.Length > 0) sb.AppendLine();
                    sb.AppendLine(section);
                    section = null;
                }

                sb.AppendLine($"{L10n.Get($"Upgrade/{_stat}")}: {_base:0.###}{_unit}({_amount:+0.###;-0.###}{_unit}), {string.Format(maxLevel, entry.MaxLevel)}");
            }

            CarrierAuthoring carrier = _spaceship.Prefab.GetComponent<CarrierAuthoring>();

            section = L10n.Get("Spec/Offensive");
            Stat(UpgradeStat.Damage, 0.0F, upgrade.Damage.Damage * 100.0F, "%");
            Stat(UpgradeStat.MaxHeat, ship.MaxHeat, upgrade.MaxHeat.MaxHeat);

            // 장착한 채로 시작하는 포탑의 발사 간격 기준
            float fireCooldown = _spaceship.Prefab.Turret != null ? _spaceship.Prefab.Turret.Spec.FireCooldown : 0.0F;
            Stat(UpgradeStat.Reload, fireCooldown, -upgrade.Reload.ReloadTime, "s");
            Stat(UpgradeStat.Reinforcement, 0.0F, upgrade.Reinforcement.Damage);
            Stat(UpgradeStat.Assembly, carrier != null ? carrier.AssemblyTime : 0.0F, -upgrade.Assembly.AssemblyTime, "s");

            section = L10n.Get("Spec/Durability");
            Stat(UpgradeStat.MaxHealth, ship.MaxHealth, upgrade.MaxHealth.MaxHealth);
            Stat(UpgradeStat.Repair, ship.Repair, upgrade.Repair.Repair);

            section = L10n.Get("Spec/Movement");
            Stat(UpgradeStat.Speed, ship.Speed, upgrade.Speed.MaxSpeed);
            Stat(UpgradeStat.Maneuver, ship.Maneuver, upgrade.Maneuver.Maneuver);

            section = L10n.Get("Spec/Radar");
            Stat(UpgradeStat.RadarRange, ship.RadarRange, upgrade.RadarRange.Range);
            Stat(UpgradeStat.RadarSweep, ship.RadarSweepSpeed, upgrade.RadarSweep.SweepSpeed);

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
