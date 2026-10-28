using GalaxyIO.DOTS.Components.Singleton;
using Unity.Entities;
using UnityEngine;

namespace GalaxyIO.DOTS.Authoring.Config
{
    /// <summary>
    /// 충돌 판정 설정. 월드당 한 개.
    /// </summary>
    public sealed class CollisionConfigAuthoring : MonoBehaviour
    {
        [SerializeField]
        [Tooltip("격자 칸 한 변 길이. 작을수록 후보는 줄고 등록할 칸은 늘어남.")]
        [Min(0.1F)]
        public float CellSize = 8.0F;

        [SerializeField]
        [Tooltip("되감기 여유분에 쓰는 우주선 최대 속력. 기체 최고 속력보다 크게 잡을 것.")]
        [Min(0.0F)]
        public float RewindSpeed = 16.0F;

        [SerializeField]
        [Tooltip("부딪혔을 때 튕겨 나가는 정도. 0이면 파고드는 속도만 사라짐.")]
        [Range(0.0F, 1.0F)]
        public float Restitution = 1.0F;

        [SerializeField]
        [Tooltip("상대 속력 1당 충돌 데미지. 두 우주선이 같은 값을 받음.")]
        [Min(0.0F)]
        public float RamDamage = 2.0F;

        [SerializeField]
        [Tooltip("상대 속력이 이보다 느린 충돌은 데미지 없음. 살짝 맞닿은 채 움직일 때 데미지가 쌓이는 것을 막음.")]
        [Min(0.0F)]
        public float RamMinSpeed = 4.0F;

        [SerializeField]
        [Tooltip("같은 모선에 딸린 함재기끼리 한 틱에 밀어내는 거리, 튕기는 속도의 최대값")]
        [Min(0.0F)]
        public float GroupRepulsion = 0.2F;

        [SerializeField]
        [Tooltip("우주선끼리 부딪혔을 때 재생할 효과음")]
        public string CrashSound;
    }

    public sealed class CollisionConfigBaker : Baker<CollisionConfigAuthoring>
    {
        public override void Bake(CollisionConfigAuthoring authoring)
        {
            Entity entity = GetEntity(TransformUsageFlags.None);

            AddComponent(entity, new CollisionConfigComponent
            {
                CellSize       = authoring.CellSize,
                RewindSpeed    = authoring.RewindSpeed,
                Restitution    = authoring.Restitution,
                RamDamage      = authoring.RamDamage,
                RamMinSpeed    = authoring.RamMinSpeed,
                GroupRepulsion = authoring.GroupRepulsion,
                CrashSound     = authoring.CrashSound,
            });
        }
    }
}
