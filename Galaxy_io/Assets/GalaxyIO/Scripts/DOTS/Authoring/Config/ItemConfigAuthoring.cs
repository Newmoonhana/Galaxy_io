using GalaxyIO.DOTS.Components.Singleton;
using Unity.Entities;
using UnityEngine;
using UnityEngine.Serialization;

namespace GalaxyIO.DOTS.Authoring.Config
{
    /// <summary>
    /// 아이템 공통 설정. 월드당 한 개.
    /// </summary>
    public sealed class ItemConfigAuthoring : MonoBehaviour
    {
        [SerializeField]
        [Tooltip("아이템을 주웠을 때 재생할 효과음. 비워두면 소리 없음.")]
        public string PickupSound;

        [SerializeField]
        [Tooltip("플레이어가 파괴될 때 가진 재료 종류마다 그 재료를 떨어뜨릴 확률.")]
        [Range(0.0F, 1.0F)]
        [FormerlySerializedAs("PlayerSlotDropChance")]
        public float PlayerKindDropChance = 0.5F;

        [SerializeField]
        [Tooltip("떨어뜨릴 재료 하나하나가 실제로 떨어질 확률. 나머지는 사라짐.")]
        [Range(0.0F, 1.0F)]
        public float PlayerStackDropChance = 0.5F;

        [Header("Turret")]

        [SerializeField]
        [Tooltip("마지막 발사 후 이 시간(초)이 지나면 포탑 열이 식기 시작함.")]
        [Min(0.0F)]
        public float NoFireStandbyTime = 3.0F;

        [SerializeField]
        [Tooltip("최대 열에서 0 까지 식는 시간(초). 무사격 대기가 끝난 뒤.")]
        [Min(0.01F)]
        public float CoolingTime = 1.5F;

        [SerializeField]
        [Tooltip("과열 시 최대 열에서 0 까지 식는 시간(초). 무사격 대기가 끝난 뒤. 0 이 될 때까지 쏠 수 없음.")]
        [Min(0.01F)]
        public float OverheatCoolingTime = 4.0F;
    }

    public sealed class ItemConfigBaker : Baker<ItemConfigAuthoring>
    {
        public override void Bake(ItemConfigAuthoring authoring)
        {
            Entity entity = GetEntity(TransformUsageFlags.None);

            AddComponent(entity, new ItemConfigComponent
            {
                PickupSound           = authoring.PickupSound,
                PlayerKindDropChance  = authoring.PlayerKindDropChance,
                PlayerStackDropChance = authoring.PlayerStackDropChance,
                NoFireStandbyTime     = authoring.NoFireStandbyTime,
                CoolingTime           = authoring.CoolingTime,
                OverheatCoolingTime   = authoring.OverheatCoolingTime,
            });
        }
    }
}
