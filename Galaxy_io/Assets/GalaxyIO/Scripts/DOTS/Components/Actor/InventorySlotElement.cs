using GalaxyIO.DOTS.Components.Singleton;
using Unity.Entities;
using Unity.Mathematics;
using Unity.NetCode;

namespace GalaxyIO.DOTS.Components.Actor
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
    /// 플레이어 인벤토리 한 칸. 버퍼 인덱스가 곧 칸 번호.
    /// 서버만 바꾸고 우주선 주인에게만 복제됨.
    /// </summary>
    [GhostComponent(OwnerSendType = SendToOwnerType.SendToOwner)]
    [InternalBufferCapacity(0)]
    public struct InventorySlotElement : IBufferElementData
    {
        [GhostField]
        public SlotType Type;

        // 빈 칸이면 None
        [GhostField]
        public ItemId Id;

        [GhostField]
        public byte Count;
    }

    /// <summary>
    /// <see cref="InventorySlotElement"/> 버퍼를 다루는 규칙 모음. 서버, 클라이언트가 같이 씀.
    /// </summary>
    public static class Inventory
    {
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
        public static int Count(DynamicBuffer<InventorySlotElement> slots, ItemId id)
        {
            int count = 0;

            for (int i = 0; i < slots.Length; i++)
            {
                if (slots[i].Id == id)
                {
                    count += slots[i].Count;
                }
            }

            return count;
        }

        /// <summary>
        /// 앞 칸부터 같은 아이템을 최대 count 개 꺼냄. 꺼낸 개수를 반환.
        /// </summary>
        public static int Remove(DynamicBuffer<InventorySlotElement> slots, ItemId id, int count)
        {
            int remaining = count;

            for (int i = 0; i < slots.Length && remaining > 0; i++)
            {
                InventorySlotElement slot = slots[i];

                if (slot.Id != id)
                {
                    continue;
                }

                int removed = math.min(remaining, slot.Count);
                slot.Count -= (byte)removed;

                if (slot.Count == 0)
                {
                    slot.Id = ItemId.None;
                }

                slots[i] = slot;
                remaining -= removed;
            }

            return count - remaining;
        }

        /// <summary>
        /// 같은 아이템 칸을 먼저 채우고 남으면 빈 일반 칸에 넣음. 넣은 개수를 반환.
        /// </summary>
        public static int Insert(
            DynamicBuffer<InventorySlotElement> slots,
            DynamicBuffer<ItemRegistryElement> registry,
            ItemId id,
            int count)
        {
            int maxStack = registry[(int)id].MaxStack;
            int remaining = count;

            for (int i = 0; i < slots.Length && remaining > 0; i++)
            {
                InventorySlotElement slot = slots[i];

                if (slot.Id != id || slot.Count >= maxStack)
                {
                    continue;
                }

                int added = math.min(remaining, maxStack - slot.Count);
                slot.Count += (byte)added;
                slots[i] = slot;
                remaining -= added;
            }

            // 장비 칸은 플레이어가 직접 옮겨 채움.
            for (int i = 0; i < slots.Length && remaining > 0; i++)
            {
                InventorySlotElement slot = slots[i];

                if (slot.Id != ItemId.None || slot.Type != SlotType.Common)
                {
                    continue;
                }

                int added = math.min(remaining, maxStack);
                slot.Id = id;
                slot.Count = (byte)added;
                slots[i] = slot;
                remaining -= added;
            }

            return count - remaining;
        }

        /// <summary>
        /// from 칸의 아이템을 최대 count 개 to 칸으로 옮김. 같은 아이템이면 합치고 넘친 만큼 from 에 남김.
        /// 다른 아이템이면 개수와 상관없이 통째로 맞바꿈. 칸 종류가 맞지 않으면 아무것도 안 함.
        /// </summary>
        public static bool Move(
            DynamicBuffer<InventorySlotElement> slots,
            DynamicBuffer<ItemRegistryElement> registry,
            int from,
            int to,
            int count)
        {
            if (from == to || from < 0 || to < 0 || from >= slots.Length || to >= slots.Length)
            {
                return false;
            }

            InventorySlotElement source = slots[from];
            InventorySlotElement target = slots[to];

            if (!registry.Contains(source.Id) || !Accepts(target.Type, registry[(int)source.Id].Type))
            {
                return false;
            }

            if (source.Id == target.Id || target.Id == ItemId.None)
            {
                int moved = math.min(
                    math.min(count, source.Count),
                    registry[(int)source.Id].MaxStack - target.Count
                );

                if (moved <= 0)
                {
                    return false;
                }

                target.Id = source.Id;
                target.Count += (byte)moved;
                source.Count -= (byte)moved;

                if (source.Count == 0)
                {
                    source.Id = ItemId.None;
                }
            }
            else
            {
                // 맞바꾸므로 from 칸도 대상 아이템을 받을 수 있어야 함.
                if (!registry.Contains(target.Id) || !Accepts(source.Type, registry[(int)target.Id].Type))
                {
                    return false;
                }

                (source.Id, target.Id) = (target.Id, source.Id);
                (source.Count, target.Count) = (target.Count, source.Count);
            }

            slots[from] = source;
            slots[to] = target;
            return true;
        }
    }
}
