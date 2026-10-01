using GalaxyIO.DOTS.Components.Actor;
using Unity.Entities;
using UnityEngine;

namespace GalaxyIO.DOTS.Authoring
{
    /// <summary>
    /// 플레이어 인벤토리 칸 구성. 일반 -> 포탑 -> 폭탄 칸 순으로 번호가 붙음.
    /// </summary>
    public sealed class InventoryAuthoring : MonoBehaviour
    {
        [SerializeField]
        [Min(0)]
        public int CommonSlots = 36;

        [SerializeField]
        [Min(0)]
        public int TurretSlots = 1;

        [SerializeField]
        [Min(0)]
        public int BombSlots = 3;
    }

    public sealed class InventoryBaker : Baker<InventoryAuthoring>
    {
        public override void Bake(InventoryAuthoring authoring)
        {
            Entity entity = GetEntity(TransformUsageFlags.Dynamic);
            DynamicBuffer<InventorySlotElement> slots = AddBuffer<InventorySlotElement>(entity);

            for (int i = 0; i < authoring.CommonSlots; i++)
            {
                slots.Add(new InventorySlotElement { Type = SlotType.Common });
            }

            for (int i = 0; i < authoring.TurretSlots; i++)
            {
                slots.Add(new InventorySlotElement { Type = SlotType.Turret });
            }

            for (int i = 0; i < authoring.BombSlots; i++)
            {
                slots.Add(new InventorySlotElement { Type = SlotType.Bomb });
            }
        }
    }
}
