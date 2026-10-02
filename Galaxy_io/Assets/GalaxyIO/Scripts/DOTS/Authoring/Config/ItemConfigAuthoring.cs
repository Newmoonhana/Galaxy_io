using GalaxyIO.DOTS.Components.Singleton;
using Unity.Entities;
using UnityEngine;

namespace GalaxyIO.DOTS.Authoring.Config
{
    /// <summary>
    /// 아이템 공통 설정. 월드당 한 개.
    /// </summary>
    public sealed class ItemConfigAuthoring : MonoBehaviour
    {
        [SerializeField]
        [Tooltip("아이템을 주웠을 때 재생할 효과음. 비워두면 소리 없음.")]
        public AudioClip PickupSound;

        [SerializeField]
        [Tooltip("플레이어가 파괴될 때 인벤토리 칸마다 그 칸을 떨어뜨릴 확률. 포탑 칸은 떨어뜨리지 않음.")]
        [Range(0.0F, 1.0F)]
        public float PlayerSlotDropChance = 0.5F;

        [SerializeField]
        [Tooltip("떨어뜨릴 칸의 아이템 하나하나가 실제로 떨어질 확률. 나머지는 사라짐.")]
        [Range(0.0F, 1.0F)]
        public float PlayerStackDropChance = 0.5F;

        [Header("Inventory")]

        [SerializeField]
        [Tooltip("플레이어 인벤토리 일반 칸 수. 칸 번호는 일반 -> 포탑 -> 폭탄 순.")]
        [Min(0)]
        public int CommonSlots = 36;

        [SerializeField]
        [Tooltip("포탑 칸 수. 첫 포탑 칸은 우주선의 SpaceshipAuthoring.Turret 을 넣은 채로 시작함.")]
        [Min(0)]
        public int TurretSlots = 1;

        [SerializeField]
        [Min(0)]
        public int BombSlots = 3;

        [Header("Turret")]

        [SerializeField]
        [Tooltip("마지막 발사 후 지난 시간을 이 값(초)까지만 셈. 과열 시스템이 쓸 값.")]
        [Min(0.0F)]
        public float NoFireStandbyTime = 3.0F;
    }

    public sealed class ItemConfigBaker : Baker<ItemConfigAuthoring>
    {
        public override void Bake(ItemConfigAuthoring authoring)
        {
            Entity entity = GetEntity(TransformUsageFlags.None);

            AddComponent(entity, new ItemConfigComponent
            {
                PickupSound           = authoring.PickupSound,
                PlayerSlotDropChance  = authoring.PlayerSlotDropChance,
                PlayerStackDropChance = authoring.PlayerStackDropChance,
                CommonSlots           = authoring.CommonSlots,
                TurretSlots           = authoring.TurretSlots,
                BombSlots             = authoring.BombSlots,
                NoFireStandbyTime     = authoring.NoFireStandbyTime,
            });
        }
    }
}
