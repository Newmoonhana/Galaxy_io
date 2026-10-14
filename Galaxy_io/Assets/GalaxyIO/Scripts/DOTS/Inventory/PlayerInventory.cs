using System;
using System.Collections.Generic;
using GalaxyIO.DOTS.Authoring;
using GalaxyIO.DOTS.Components.Singleton;

namespace GalaxyIO.DOTS.Inventory
{
    /// <summary>
    /// 플레이어가 가진 재료 개수, 잠금 해제한 포탑과 그 규칙. 서버가 원본을, 주인 클라이언트가 사본을 가짐.
    /// 칸 없이 <see cref="Ingredients"/> 종류마다 개수만 셈. 사망하면 인벤토리와 함께 모두 사라짐.
    /// </summary>
    public sealed class PlayerInventory
    {
        // 인벤토리에 쌓이는 재료. 이 순서대로 보여줌.
        public static readonly ItemId[] Ingredients = { ItemId.PowerCell, ItemId.OverchargedPowerCell, ItemId.Reactor };

        // 재료 하나가 쌓이는 최대 개수. RPC 크기에 맞춤.
        public const int MaxCount = ushort.MaxValue;

        // ItemId -> 개수
        private readonly int[] counts;

        // 주인에게 아직 보내지 않은 재료. 서버만 씀.
        private readonly bool[] dirty;

        // 잠금 해제한 포탑. 다시 장착할 때는 재료를 쓰지 않음.
        private readonly HashSet<ItemId> unlocked = new HashSet<ItemId>();

        // 주인에게 아직 보내지 않은 잠금 해제. 서버만 씀.
        private readonly Stack<ItemId> unsentUnlocks = new Stack<ItemId>();

        public PlayerInventory()
        {
            int length = 0;

            foreach (ItemId id in Ingredients)
            {
                length = Math.Max(length, (int)id + 1);
            }

            counts = new int[length];
            dirty = new bool[length];
        }

        public static bool IsIngredient(ItemId id) => Array.IndexOf(Ingredients, id) >= 0;

        public int Count(ItemId id) => (int)id < counts.Length ? counts[(int)id] : 0;

        public void Set(ItemId id, int count)
        {
            if (!IsIngredient(id))
            {
                return;
            }

            counts[(int)id] = Math.Clamp(count, 0, MaxCount);
            dirty[(int)id] = true;
        }

        /// <summary>
        /// 재료가 바뀌었으면 true 를 반환하고 표시를 지움.
        /// </summary>
        public bool TakeDirty(ItemId id)
        {
            bool changed = dirty[(int)id];
            dirty[(int)id] = false;
            return changed;
        }

        public bool IsUnlocked(ItemId id) => unlocked.Contains(id);

        public void Unlock(ItemId id)
        {
            if (unlocked.Add(id))
            {
                unsentUnlocks.Push(id);
            }
        }

        /// <summary>
        /// 아직 보내지 않은 잠금 해제를 하나 꺼냄. 없으면 false.
        /// </summary>
        public bool TakeUnlock(out ItemId id) => unsentUnlocks.TryPop(out id);

        /// <summary>
        /// 재료가 아니면 넣지 않음. 넣은 개수를 반환.
        /// </summary>
        public int Insert(ItemId id, int count)
        {
            if (!IsIngredient(id))
            {
                return 0;
            }

            int added = Math.Min(count, MaxCount - Count(id));
            Set(id, Count(id) + added);
            return added;
        }

        /// <summary>
        /// 재료를 모두 가지고 있는지 여부. 비어 있는 재료 항목은 건너뜀.
        /// </summary>
        public bool HasIngredients(List<RecipeAsset.Ingredient> ingredients)
        {
            foreach (RecipeAsset.Ingredient ingredient in ingredients)
            {
                if (ingredient.Item != null && Count(ingredient.Item.Id) < ingredient.Count)
                {
                    return false;
                }
            }

            return true;
        }

        /// <summary>
        /// 재료를 모두 가지고 있으면 소모하고 true.
        /// </summary>
        public bool Consume(List<RecipeAsset.Ingredient> ingredients)
        {
            if (!HasIngredients(ingredients))
            {
                return false;
            }

            foreach (RecipeAsset.Ingredient ingredient in ingredients)
            {
                if (ingredient.Item != null)
                {
                    Set(ingredient.Item.Id, Count(ingredient.Item.Id) - ingredient.Count);
                }
            }

            return true;
        }
    }
}
