using System;
using GalaxyIO.DOTS.Authoring;
using GalaxyIO.DOTS.Authoring.Config;
using GalaxyIO.DOTS.Components.Singleton;

namespace GalaxyIO.DOTS.Inventory
{
    /// <summary>
    /// 인벤토리 칸 종류. 칸에 넣을 수 있는 <see cref="ItemType"/>을 정함.
    /// </summary>
    public enum SlotType : byte
    {
        // 모든 아이템
        Common = 0,

        // 완성된 포탑만
        Turret = 1,

        // 폭탄만
        Bomb = 2,
    }

    /// <summary>
    /// 인벤토리 한 칸.
    /// </summary>
    public struct ItemSlot
    {
        public SlotType Type;

        // 빈 칸이면 None
        public ItemId Id;

        public int Count;
    }

    /// <summary>
    /// 플레이어 인벤토리와 그 규칙. 서버가 원본을, 주인 클라이언트가 사본을 가짐.
    /// 칸 번호는 일반 -> 포탑 -> 폭탄 순. 첫 포탑 칸의 아이템이 우주선에 장착됨.
    /// </summary>
    public sealed class PlayerInventory
    {
        private readonly ItemSlot[] slots;

        // 주인에게 아직 보내지 않은 칸. 서버만 씀.
        private readonly bool[] dirty;

        public PlayerInventory(in ItemConfigComponent config, ItemId startTurret = ItemId.None)
        {
            slots = new ItemSlot[config.CommonSlots + config.TurretSlots + config.BombSlots];
            dirty = new bool[slots.Length];

            for (int i = 0; i < slots.Length; i++)
            {
                slots[i].Type = i < config.CommonSlots ? SlotType.Common
                    : i < config.CommonSlots + config.TurretSlots ? SlotType.Turret
                    : SlotType.Bomb;
            }

            if (startTurret != ItemId.None && config.TurretSlots > 0)
            {
                Set(config.CommonSlots, startTurret, 1);
            }
        }

        public int Length => slots.Length;

        public ItemSlot this[int index] => slots[index];

        public void Set(int index, ItemId id, int count)
        {
            slots[index].Id = count > 0 ? id : ItemId.None;
            slots[index].Count = Math.Max(count, 0);
            dirty[index] = true;
        }

        /// <summary>
        /// 칸이 바뀌었으면 true 를 반환하고 표시를 지움.
        /// </summary>
        public bool TakeDirty(int index)
        {
            bool changed = dirty[index];
            dirty[index] = false;
            return changed;
        }

        public static bool Accepts(SlotType slot, ItemType item)
        {
            switch (slot)
            {
                case SlotType.Turret:
                    return item == ItemType.Turret;
                case SlotType.Bomb:
                    return item == ItemType.Bomb;
                default:
                    return true;
            }
        }

        /// <summary>
        /// 모든 칸에 든 같은 아이템 개수의 합.
        /// </summary>
        public int Count(ItemId id)
        {
            int count = 0;

            foreach (ItemSlot slot in slots)
            {
                if (slot.Id == id)
                {
                    count += slot.Count;
                }
            }

            return count;
        }

        /// <summary>
        /// 앞 칸부터 같은 아이템을 최대 count 개 꺼냄.
        /// </summary>
        public void Remove(ItemId id, int count)
        {
            for (int i = 0; i < slots.Length && count > 0; i++)
            {
                if (slots[i].Id != id)
                {
                    continue;
                }

                int removed = Math.Min(count, slots[i].Count);
                Set(i, id, slots[i].Count - removed);
                count -= removed;
            }
        }

        /// <summary>
        /// 같은 아이템 칸을 먼저 채우고 남으면 빈 일반 칸에 넣음. 넣은 개수를 반환.
        /// </summary>
        public int Insert(ItemRegistryAsset registry, ItemId id, int count)
        {
            ItemAsset item = registry.Find(id);

            if (item == null)
            {
                return 0;
            }

            int remaining = count;

            for (int i = 0; i < slots.Length && remaining > 0; i++)
            {
                if (slots[i].Id != id || slots[i].Count >= item.Stacking)
                {
                    continue;
                }

                int added = Math.Min(remaining, item.Stacking - slots[i].Count);
                Set(i, id, slots[i].Count + added);
                remaining -= added;
            }

            // 장비 칸은 플레이어가 직접 옮겨 채움.
            for (int i = 0; i < slots.Length && remaining > 0; i++)
            {
                if (slots[i].Id != ItemId.None || slots[i].Type != SlotType.Common)
                {
                    continue;
                }

                int added = Math.Min(remaining, item.Stacking);
                Set(i, id, added);
                remaining -= added;
            }

            return count - remaining;
        }

        /// <summary>
        /// from 칸의 아이템을 최대 count 개 to 칸으로 옮김. 같은 아이템이면 합치고 넘친 만큼 from 에 남김.
        /// 다른 아이템이면 개수와 상관없이 통째로 맞바꿈. 칸 종류가 맞지 않으면 아무것도 안 함.
        /// </summary>
        public bool Move(ItemRegistryAsset registry, int from, int to, int count)
        {
            if (from == to || from < 0 || to < 0 || from >= slots.Length || to >= slots.Length)
            {
                return false;
            }

            ItemSlot source = slots[from];
            ItemSlot target = slots[to];
            ItemAsset sourceItem = registry.Find(source.Id);

            if (sourceItem == null || !Accepts(target.Type, sourceItem.Type))
            {
                return false;
            }

            if (source.Id == target.Id || target.Id == ItemId.None)
            {
                int moved = Math.Min(Math.Min(count, source.Count), sourceItem.Stacking - target.Count);

                if (moved <= 0)
                {
                    return false;
                }

                Set(to, source.Id, target.Count + moved);
                Set(from, source.Id, source.Count - moved);
                return true;
            }

            // 맞바꾸므로 from 칸도 대상 아이템을 받을 수 있어야 함.
            ItemAsset targetItem = registry.Find(target.Id);

            if (targetItem == null || !Accepts(source.Type, targetItem.Type))
            {
                return false;
            }

            Set(to, source.Id, source.Count);
            Set(from, target.Id, target.Count);
            return true;
        }

        /// <summary>
        /// 제작법의 재료를 모두 가지고 있는지 여부.
        /// </summary>
        public bool HasIngredients(RecipeAsset recipe)
        {
            foreach (RecipeAsset.Ingredient ingredient in recipe.Ingredients)
            {
                if (ingredient.Item != null && Count(ingredient.Item.Id) < ingredient.Count)
                {
                    return false;
                }
            }

            return true;
        }

        /// <summary>
        /// index 칸의 설계도 한 장과 재료를 소모해 결과물을 넣음.
        /// 재료가 모자라거나 결과물을 넣을 칸이 없으면 아무것도 안 함.
        /// </summary>
        public void Craft(ItemRegistryAsset registry, int index)
        {
            if (index < 0 || index >= slots.Length
                || !(registry.Find(slots[index].Id) is BlueprintItemAsset blueprint)
                || blueprint.Recipe == null
                || blueprint.Recipe.Result == null
                || !HasIngredients(blueprint.Recipe))
            {
                return;
            }

            // 소모한 뒤 빈 칸이 생길 수 있으므로 먼저 소모해 보고, 결과물이 안 들어가면 되돌림.
            var backup = (ItemSlot[])slots.Clone();

            Set(index, slots[index].Id, slots[index].Count - 1);

            foreach (RecipeAsset.Ingredient ingredient in blueprint.Recipe.Ingredients)
            {
                if (ingredient.Item != null)
                {
                    Remove(ingredient.Item.Id, ingredient.Count);
                }
            }

            if (Insert(registry, blueprint.Recipe.Result.Id, 1) == 0)
            {
                Array.Copy(backup, slots, slots.Length);
            }
        }

        /// <summary>
        /// 첫 포탑 칸의 포탑 아이템. 없으면 None.
        /// </summary>
        public ItemId EquippedTurret(ItemRegistryAsset registry)
        {
            foreach (ItemSlot slot in slots)
            {
                if (slot.Type != SlotType.Turret)
                {
                    continue;
                }

                ItemAsset item = registry.Find(slot.Id);
                return item != null && item.Type == ItemType.Turret ? slot.Id : ItemId.None;
            }

            return ItemId.None;
        }
    }
}
