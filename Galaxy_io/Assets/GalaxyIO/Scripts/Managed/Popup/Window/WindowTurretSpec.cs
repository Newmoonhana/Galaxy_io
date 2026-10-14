using System.Collections.Generic;
using System.Text;
using GalaxyIO.DOTS.Authoring;
using GalaxyIO.DOTS.Components.Actor;
using GalaxyIO.DOTS.Components.Singleton;
using GalaxyIO.DOTS.Inventory;
using Newmoonhana.Runtime.Tools.PopupUI.Window;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace GalaxyIO.Tools.PopupUI.Window
{
    /// <summary>
    /// 포탑 상세 사양 창. 아이콘, 필요 재료, 이름, 사양, 설명, 장착 버튼.
    /// 잠금 해제하지 않은 포탑은 장착 버튼이 재료를 써서 잠금 해제하고 장착함.
    /// 사양은 포탑 에셋과 총구의 투사체에서 읽음.
    /// </summary>
    public class WindowTurretSpec : WindowSpecBase
    {
        [Header("Turret")]
        [SerializeField] private Image icon_img;

        [Tooltip("필요 재료 줄 목록. 템플릿은 WalletRow.prefab")]
        [SerializeField] private WindowSlotList ingredient_list;

        [SerializeField] private TextMeshProUGUI desc_tmp;

        private RecipeAsset recipe;

        public ItemId Turret => recipe != null ? recipe.Result.Id : ItemId.None;

        /// <summary>
        /// 열 때 호출. 보여줄 포탑의 제작법을 정함.
        /// </summary>
        public void SetRecipe(RecipeAsset _recipe)
        {
            recipe = _recipe;

            ItemAsset result = _recipe.Result;
            name_tmp.text = L10n.Get($"Item/{result.Id}");
            icon_img.sprite = result.Sprite;
            desc_tmp.text = L10n.Get($"Tooltip/{result.Id}");
            spec_tmp.text = string.Empty;

            // 사양은 첫 번째로 쏘는 총구 기준
            if (result is TurretItemAsset turret && turret.TurretSpecs.Find(s => s.Armed) is { Armed: true } spec)
            {
                var projectile = spec.Projectile.GetComponent<ProjectileAuthoring>();
                bool isBeam = projectile != null && projectile.Beam != null;
                var sb = new StringBuilder();

                sb.AppendLine($"{L10n.Get("Turret/Type")}: {L10n.Get(isBeam ? "Turret/Beam" : "Turret/Projectile")}");

                // 한 번에 여러 발을 쏘면 발당 데미지와 발 수
                sb.Append($"{L10n.Get("Turret/Damage")}: {spec.Damage:0.###}");
                if (spec.ProjectileCount > 1) sb.Append($" x{spec.ProjectileCount}");

                // 폭발 데미지는 거리에 따라 0 ~ 직격 데미지
                if (projectile != null && projectile.ExplosionRadius > 0.0F)
                    sb.Append($", {L10n.Get("Turret/Explosion")}: 0~{spec.Damage:0.###}");

                sb.AppendLine();
                sb.AppendLine($"{L10n.Get("Turret/RPS")}: {spec.RPS:0.###}");
                sb.AppendLine($"{L10n.Get("Turret/Accuracy")}: {spec.Accuracy:0.###}");

                if (projectile is StraightforwardProjectileAuthoring straight)
                    sb.AppendLine($"{L10n.Get("Turret/Velocity")}: {straight.Speed:0.###}");

                string heatUnit = L10n.Get(turret.HeatMode == TurretHeatMode.PerShot ? "Turret/PerShot" : "Turret/PerSecond");
                sb.Append($"{L10n.Get("Turret/Heat")}: {turret.Heat:0.###}{heatUnit}");

                spec_tmp.text = sb.ToString();
            }

            List<RecipeAsset.Ingredient> ingredients = _recipe.Ingredients;
            ingredient_list.SetCount(ingredients.Count);

            for (int i = 0; i < ingredients.Count; i++)
            {
                ItemAsset item = ingredients[i].Item;
                Element(i)?.SetItem(item != null ? item.Sprite : null,
                    item != null ? L10n.Get($"Item/{item.Id}") : string.Empty);
            }
        }

        /// <summary>
        /// 창이 열려 있는 동안 매 프레임 호출됨.
        /// </summary>
        /// <param name="_equipped">고른 포탑 칸에 장착한 포탑</param>
        public void Refresh(PlayerInventory _inventory, ItemId _equipped)
        {
            List<RecipeAsset.Ingredient> ingredients = recipe.Ingredients;

            for (int i = 0; i < ingredients.Count; i++)
            {
                if (ingredients[i].Item == null) continue;

                Element(i)?.SetAmount(ingredients[i].Count, _inventory.Count(ingredients[i].Item.Id) < ingredients[i].Count);
            }

            ItemId turret = recipe.Result.Id;

            if (_equipped == turret) SetEquip("Shop/Equipped", false);
            else if (_inventory.IsUnlocked(turret)) SetEquip("Shop/Equip", true);
            else SetEquip("Shop/Unlock", _inventory.HasIngredients(ingredients));
        }

        private IngredientElement Element(int _index) => ingredient_list.Element(_index) as IngredientElement;
    }
}
