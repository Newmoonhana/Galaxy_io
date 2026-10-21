using GalaxyIO.DOTS.Authoring;
using GalaxyIO.DOTS.Client.Systems;
using Unity.Entities;

namespace GalaxyIO.DOTS.Components.Actor
{
    /// <summary>
    /// <see cref="ProjectileAuthoring.Beam"/>의 DOTS 구조체. 이 투사체를 쏘는 동안 총구에서 뻗는 광선 연출.
    /// </summary>
    public struct ProjectileBeamComponent : IComponentData
    {
        // 광선 프리팹(BeamAuthoring)
        public Entity Prefab;

        // 광선 최대 길이. 투사체 사거리.
        public float Range;
    }

    /// <summary>
    /// <see cref="BeamAuthoring"/>의 DOTS 구조체. 총구 하나에 붙은 광선. 클라이언트만 씀.
    /// <see cref="ProjectileBeamSystem"/>이 단계(발사 -> 충전 -> 폭발 -> 사격 -> 중지)마다 크기를 바꿈.
    /// </summary>
    public struct BeamComponent : IComponentData
    {
        // 광선 몸통, 양 끝의 에너지 구(SpriteRenderer). 없으면 Entity.Null.
        public Entity Body;
        public Entity StartBall;
        public Entity EndBall;

        // 몸통과 양 끝 구를 함께 감싸는 번짐(SpriteRenderer). 없으면 Entity.Null.
        public Entity Glow;

        // 사격 중 양 끝에서 퍼지는 가루, 광선을 따라 흐르는 가루, 폭발 고리, 폭발 가루, 충전 중 빨려드는 가루(ParticleSystem).
        // 없으면 Entity.Null.
        public Entity StartDust;
        public Entity EndDust;
        public Entity Flow;
        public Entity BurstRing;
        public Entity BurstDust;
        public Entity ChargeDust;

        // 길이가 늘어나는 속력
        public float GrowSpeed;

        // 사격 중 광선 폭
        public float Width;

        // 에너지 구 지름. 발사 순간, 충전 끝, 사격 중.
        public float LaunchSize;
        public float WindupSize;
        public float BallSize;

        // 충전 중 구가 떨리는 정도. 구 지름 대비 비율.
        public float ChargeJitter;

        // 폭발 순간 광선과 구가 부푸는 배율과 원래대로 돌아오는 시간(초)
        public float BurstScale;
        public float BurstTime;

        // 사격을 멈춰 사라지기까지의 시간(초)
        public float NarrowTime;

        // 쏘는 우주선과 총구 번호. 조종 포탑 총구 수 이상이면 그만큼 뺀 순번의 자동 포탑.
        public Entity Owner;
        public int Muzzle;

        // 쏘는 투사체 프리팹. 장착 포탑이 바뀌면 사격을 멈춤.
        public Entity Projectile;

        // 현재 길이
        public float Length;

        // 충전 진행률(0 ~ 1). 충전 단계의 구 지름을 정함.
        public float Charge;

        // 중지 후 남은 굵기 비율(0 ~ 1). 발사/사격 중에는 1.
        public float Thickness;

        // 폭발 후 남은 부푼 시간(초)
        public float BurstRemaining;

        // 폭발해 사격 단계에 들어섬. 꺼져 있으면 충전 단계.
        public bool Fired;
    }
}
