using GalaxyIO.DOTS.Components.Actor;
using GalaxyIO.DOTS.Components.Singleton;
using Unity.Entities;
using UnityEngine;

namespace GalaxyIO.DOTS.Authoring
{
    /// <summary>
    /// 플레이어 인벤토리 칸 구성. 일반 -> 포탑 -> 폭탄 칸 순으로 번호가 붙음.
    /// 첫 포탑 칸의 아이템이 우주선에 장착됨. 첫 포탑 칸은 <see cref="SpaceshipAuthoring.Turret"/>을 넣은 채로 시작함.
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

            SpaceshipAuthoring spaceship = GetComponent<SpaceshipAuthoring>();
            DependsOn(spaceship != null ? spaceship.Turret : null);

            ItemId startTurret = spaceship != null && spaceship.Turret != null ? spaceship.Turret.Id : ItemId.None;

            for (int i = 0; i < authoring.TurretSlots; i++)
            {
                bool first = i == 0 && startTurret != ItemId.None;

                slots.Add(new InventorySlotElement
                {
                    Type  = SlotType.Turret,
                    Id    = first ? startTurret : ItemId.None,
                    Count = first ? (byte)1 : (byte)0,
                });
            }

            for (int i = 0; i < authoring.BombSlots; i++)
            {
                slots.Add(new InventorySlotElement { Type = SlotType.Bomb });
            }
        }
    }
}
