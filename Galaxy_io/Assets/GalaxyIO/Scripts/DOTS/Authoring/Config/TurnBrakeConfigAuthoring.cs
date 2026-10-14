using GalaxyIO.DOTS.Components.Singleton;
using Unity.Entities;
using UnityEngine;

namespace GalaxyIO.DOTS.Authoring.Config
{
    /// <summary>
    /// 선회 제동 설정. 월드당 한 개.
    /// </summary>
    public sealed class TurnBrakeConfigAuthoring : MonoBehaviour
    {
        [SerializeField]
        [Tooltip("기준 회전속도(도/초). 기준 속력에서 이 속도로 돌 때 Deceleration 만큼 감속함.")]
        [Min(1.0F)]
        public float TurnRate = 360.0F;

        [SerializeField]
        [Tooltip("기준 속력에서 기준 회전속도로 돌 때 초당 줄어드는 속력")]
        [Min(0.0F)]
        public float Deceleration = 30.0F;

        [SerializeField]
        [Tooltip("곡선의 휘는 정도. 1이면 직선, 클수록 느린 선회는 덜 깎이고 빠른 선회에서 급격히 커짐.")]
        [Min(1.0F)]
        public float Exponent = 2.0F;

        [SerializeField]
        [Tooltip("제동이 걸리기 시작하는 비행 속력. 이 이하에서는 감속하지 않음.")]
        [Min(0.0F)]
        public float StartSpeed = 30.0F;

        [SerializeField]
        [Tooltip("기준 속력. StartSpeed 부터 선형으로 커져 이 속력에서 Deceleration 그대로 걸리고, 그 위로도 계속 커짐.")]
        [Min(0.0F)]
        public float ReferenceSpeed = 60.0F;

        [SerializeField]
        [Tooltip("회전 관성. 선회 제동이 셀수록 속도가 기수를 덜 따라 돌아 돌기 전 방향으로 밀려남. 0이면 밀리지 않고, 1이면 기준 제동에서 기수를 전혀 따라 돌지 않음.")]
        [Min(0.0F)]
        public float Inertia = 1.0F;

        [SerializeField]
        [Tooltip("기수와 어긋난 옆 방향 속도가 초당 줄어드는 양")]
        [Min(0.0F)]
        public float InertiaDeceleration = 20.0F;

        [SerializeField]
        [Tooltip("뒤처진 속도 방향이 기수 방향을 따라 도는 속도(도/초)")]
        [Min(0.0F)]
        public float InertiaTurnRate = 120.0F;
    }

    public sealed class TurnBrakeConfigBaker : Baker<TurnBrakeConfigAuthoring>
    {
        public override void Bake(TurnBrakeConfigAuthoring authoring)
        {
            Entity entity = GetEntity(TransformUsageFlags.None);

            AddComponent(entity, new TurnBrakeConfigComponent
            {
                TurnRate     = authoring.TurnRate,
                Deceleration = authoring.Deceleration,
                Exponent     = authoring.Exponent,
                StartSpeed   = authoring.StartSpeed,

                // StartSpeed 와 같거나 작으면 나눌 수 없음.
                ReferenceSpeed = Mathf.Max(authoring.ReferenceSpeed, authoring.StartSpeed + 0.01F),

                Inertia             = authoring.Inertia,
                InertiaDeceleration = authoring.InertiaDeceleration,
                InertiaTurnRate     = authoring.InertiaTurnRate,
            });
        }
    }
}
