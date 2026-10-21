using GalaxyIO.DOTS.Components.Actor;
using Unity.Entities;
using UnityEngine;

namespace GalaxyIO.DOTS.Authoring
{
    /// <summary>
    /// 총구에서 기수 방향으로 뻗는 광선 연출. 로컬 +Y 가 광선 방향.
    /// 발사하면 총구에 에너지 구가 생겨 충전 동안 커지고, 사격이 시작되면 터지며 광선이 뻗음.
    /// </summary>
    public sealed class BeamAuthoring : MonoBehaviour
    {
        [SerializeField]
        [Tooltip("광선 몸통. 자식 SpriteRenderer. Draw Mode 는 Sliced, 스프라이트 Pivot 은 아래쪽.")]
        public SpriteRenderer Body;

        [SerializeField]
        [Tooltip("총구 쪽 에너지 구. 자식 SpriteRenderer. 크기 1 일 때 지름 1.")]
        public SpriteRenderer StartBall;

        [SerializeField]
        [Tooltip("광선 끝 쪽 에너지 구. 자식 SpriteRenderer. 크기 1 일 때 지름 1.")]
        public SpriteRenderer EndBall;

        [SerializeField]
        [Tooltip("몸통과 양 끝 구를 함께 감싸는 번짐. 자식 SpriteRenderer. 정사각형 스프라이트, Draw Mode 는 Sliced, " +
                 "머티리얼은 Sprite-BeamGlow. 몸통과 구보다 뒤에 그릴 것. 비우면 없음.")]
        public SpriteRenderer Glow;

        [SerializeField]
        [Tooltip("사격 중 총구 쪽 구에서 퍼지는 가루. 자식 ParticleSystem. 사격 중에만 방출. 비우면 없음.")]
        public ParticleSystem StartDust;

        [SerializeField]
        [Tooltip("사격 중 광선 끝 쪽 구에서 퍼지는 가루. 자식 ParticleSystem. 사격 중에만 방출. 비우면 없음.")]
        public ParticleSystem EndDust;

        [SerializeField]
        [Tooltip("사격 중 광선을 따라 끝 쪽으로 흐르는 가루. 자식 ParticleSystem. Local 공간, Box 모양. " +
                 "Box 폭(Scale X)은 흐름 폭, 길이는 광선 길이에 맞춤. 광선 끝을 넘은 가루는 지움. 비우면 없음.")]
        public ParticleSystem Flow;

        [SerializeField]
        [Tooltip("폭발할 때 한 번 재생하는 고리. 자식 ParticleSystem. 비우면 없음.")]
        public ParticleSystem BurstRing;

        [SerializeField]
        [Tooltip("폭발할 때 한 번 재생하는 가루. 자식 ParticleSystem. 비우면 없음.")]
        public ParticleSystem BurstDust;

        [SerializeField]
        [Tooltip("충전 중 구 둘레에서 생겨 총구로 빨려드는 가루. 자식 ParticleSystem. Local 공간. 충전 중에만 방출. 비우면 없음.")]
        public ParticleSystem ChargeDust;

        [SerializeField]
        [Tooltip("길이가 늘어나는 속력")]
        [Min(0.0F)]
        public float GrowSpeed = 120.0F;

        [SerializeField]
        [Tooltip("사격 중 광선 폭(SpriteRenderer.size.x)")]
        [Min(0.0F)]
        public float Width = 1.0F;

        [SerializeField]
        [Tooltip("발사 순간 에너지 구 지름")]
        [Min(0.0F)]
        public float LaunchSize = 0.2F;

        [SerializeField]
        [Tooltip("충전이 끝났을 때 에너지 구 지름")]
        [Min(0.0F)]
        public float WindupSize = 0.6F;

        [SerializeField]
        [Tooltip("충전 중 구가 떨리는 정도. 구 지름 대비 비율. 매 프레임 크기와 위치가 이만큼 무작위로 흔들림.")]
        [Min(0.0F)]
        public float ChargeJitter;

        [SerializeField]
        [Tooltip("사격 중 양 끝 에너지 구 지름")]
        [Min(0.0F)]
        public float BallSize = 0.4F;

        [SerializeField]
        [Tooltip("폭발 순간 광선 폭과 구 지름에 곱하는 배율")]
        [Min(1.0F)]
        public float BurstScale = 3.0F;

        [SerializeField]
        [Tooltip("폭발 배율에서 원래 크기로 돌아오는 시간(초)")]
        [Min(0.0F)]
        public float BurstTime = 0.15F;

        [SerializeField]
        [Tooltip("사격을 멈춰 사라지기까지의 시간(초)")]
        [Min(0.0F)]
        public float NarrowTime = 0.2F;
    }

    public sealed class BeamBaker : Baker<BeamAuthoring>
    {
        public override void Bake(BeamAuthoring authoring)
        {
            AddComponent(GetEntity(TransformUsageFlags.Dynamic), new BeamComponent
            {
                Body       = authoring.Body != null ? GetEntity(authoring.Body, TransformUsageFlags.Dynamic) : Entity.Null,
                StartBall  = authoring.StartBall != null ? GetEntity(authoring.StartBall, TransformUsageFlags.Dynamic) : Entity.Null,
                EndBall    = authoring.EndBall != null ? GetEntity(authoring.EndBall, TransformUsageFlags.Dynamic) : Entity.Null,
                Glow       = authoring.Glow != null ? GetEntity(authoring.Glow, TransformUsageFlags.Dynamic) : Entity.Null,
                StartDust  = authoring.StartDust != null ? GetEntity(authoring.StartDust, TransformUsageFlags.Dynamic) : Entity.Null,
                EndDust    = authoring.EndDust != null ? GetEntity(authoring.EndDust, TransformUsageFlags.Dynamic) : Entity.Null,
                Flow       = authoring.Flow != null ? GetEntity(authoring.Flow, TransformUsageFlags.Dynamic) : Entity.Null,
                BurstRing  = authoring.BurstRing != null ? GetEntity(authoring.BurstRing, TransformUsageFlags.Dynamic) : Entity.Null,
                BurstDust  = authoring.BurstDust != null ? GetEntity(authoring.BurstDust, TransformUsageFlags.Dynamic) : Entity.Null,
                ChargeDust = authoring.ChargeDust != null ? GetEntity(authoring.ChargeDust, TransformUsageFlags.Dynamic) : Entity.Null,
                GrowSpeed  = authoring.GrowSpeed,
                Width      = authoring.Width,
                LaunchSize = authoring.LaunchSize,
                WindupSize = authoring.WindupSize,
                ChargeJitter = authoring.ChargeJitter,
                BallSize   = authoring.BallSize,
                BurstScale = authoring.BurstScale,
                BurstTime  = authoring.BurstTime,
                NarrowTime = authoring.NarrowTime,
            });
        }
    }
}
