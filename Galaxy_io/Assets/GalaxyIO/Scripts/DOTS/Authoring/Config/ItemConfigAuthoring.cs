using GalaxyIO.DOTS.Components.Singleton;
using Unity.Entities;
using Unity.Mathematics;
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

        [Header("Auto Turret")]

        [SerializeField]
        [Tooltip("대상이 없을 때 시야각 끝에서 끝으로 훑는 속도(도/초).")]
        [Min(0.0F)]
        public float AutoTurretSweepSpeed = 30.0F;

        [SerializeField]
        [Tooltip("훑다가 시야각 끝에 닿았을 때 기다리는 시간(초).")]
        [Min(0.0F)]
        public float AutoTurretSweepWait = 1.0F;

        [SerializeField]
        [Tooltip("사격을 허용하는 조준 오차(도). 포탑 방향과 대상 방향 사이 각도.")]
        [Range(0.0F, 180.0F)]
        public float AutoTurretAimAngle = 5.0F;
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
                AutoTurretSweepSpeed  = math.radians(authoring.AutoTurretSweepSpeed),
                AutoTurretSweepWait   = authoring.AutoTurretSweepWait,
                AutoTurretAimAngle    = math.radians(authoring.AutoTurretAimAngle),
            });
        }
    }
}
