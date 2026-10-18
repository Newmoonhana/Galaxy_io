using GalaxyIO.DOTS.Components.Actor;
using Unity.Entities;
using UnityEngine;

namespace GalaxyIO.DOTS.Authoring
{
    /// <summary>
    /// 함재기를 만들어 내보내는 모선의 정의. 모선은 피해, 포탑 열 대신 함재기 강화, 생산성 증대를 강화함.
    /// </summary>
    [RequireComponent(typeof(SpaceshipAuthoring))]
    public sealed class CarrierAuthoring : MonoBehaviour
    {
        [SerializeField]
        [Tooltip("출격시킬 함재기")]
        public CarriedCraftAuthoring CarriedCraft;

        [SerializeField]
        [Tooltip("함재기 최대 수. 모선 안과 밖을 합친 수.")]
        [Range(1, 255)]
        public int Capacity = 8;

        [SerializeField]
        [Tooltip("처음에 가지고 있는 함재기 수")]
        [Min(0)]
        public int InitialCount = 4;

        [SerializeField]
        [Tooltip("함재기 한 대를 만드는 시간(초). 함재기가 최대 수보다 적으면 만듦.")]
        [Min(0.1F)]
        public float AssemblyTime = 8.0F;

        [SerializeField]
        [Tooltip("함재기를 한 대씩 내보내는 간격(초)")]
        [Min(0.0F)]
        public float LaunchInterval = 0.75F;

        [SerializeField]
        [Tooltip("명령을 다시 내릴 수 있기까지의 시간(초)")]
        [Min(0.0F)]
        public float OrderCooldown = 1.0F;

        [SerializeField]
        [Tooltip("귀환 명령을 받은 함재기가 출발하기까지 기다리는 최대 시간(초). 함재기마다 0 ~ 이 값 중에서 뽑음.")]
        [Min(0.0F)]
        public float ReturnDelay = 1.5F;
    }

    public sealed class CarrierBaker : Baker<CarrierAuthoring>
    {
        public override void Bake(CarrierAuthoring authoring)
        {
            if (authoring.CarriedCraft == null)
            {
                Debug.LogWarning("Carrier: 함재기가 비어 있음.", authoring);
                return;
            }

            Entity entity = GetEntity(TransformUsageFlags.Dynamic);
            int stored = Mathf.Min(authoring.InitialCount, authoring.Capacity);

            AddComponent(entity, new CarrierComponent
            {
                CraftPrefab       = GetEntity(authoring.CarriedCraft, TransformUsageFlags.Dynamic),
                Capacity          = authoring.Capacity,
                AssemblyTime      = authoring.AssemblyTime,
                LaunchInterval    = authoring.LaunchInterval,
                OrderCooldown     = authoring.OrderCooldown,
                ReturnDelay       = authoring.ReturnDelay,
                Stored            = (byte)stored,
                Total             = (byte)stored,
                AssemblyRemaining = authoring.AssemblyTime,
            });
        }
    }
}
