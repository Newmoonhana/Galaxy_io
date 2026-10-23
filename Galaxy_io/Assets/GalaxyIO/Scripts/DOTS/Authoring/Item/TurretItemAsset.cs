using System;
using GalaxyIO.DOTS.Components.Actor;
using GalaxyIO.DOTS.Components.Singleton;
using Unity.Mathematics;
using UnityEngine;

namespace GalaxyIO.DOTS.Authoring
{
    /// <summary>
    /// 포탑 아이템. 포탑 상점에서 사면 우주선 총구에 장착됨.
    /// 포탑 사양은 우주선의 모든 총구에 똑같이 적용됨. 총구 수는 우주선이 정함.
    /// </summary>
    [CreateAssetMenu(fileName = "SO_Item_Turret", menuName = "GalaxyIO/Turret Item")]
    public sealed class TurretItemAsset : ItemAsset
    {
        [Serializable]
        public struct TurretSpec
        {
            [Tooltip("발사체. 비우면 쏘지 않음.")]
            public GameObject Projectile;

            [Tooltip("초당 발사 수")]
            [Min(0.0F)]
            public float RPS;

            [Tooltip("총구가 여럿일 때 첫 총구가 쏜 뒤 다음 총구가 차례로 쏘는 간격(초). " +
                     "0 이면 모든 총구가 함께 쏨. 뒤 총구는 방아쇠를 떼도 마저 쏨.")]
            [Min(0.0F)]
            public float MuzzleDelay;

            [Tooltip("총구 하나가 한 번에 쏘는 투사체 수. 각자 탄퍼짐, 속력 분포를 따로 받음. 0 은 1 로 취급.")]
            [Range(0, 32)]
            public int ProjectileCount;

            [Tooltip("투사체 속력 분포. 발사 속력에 1 ± 이 값 사이의 배율을 곱함.")]
            [Range(0.0F, 1.0F)]
            public float SpeedSpread;

            [Tooltip("정확도 (0 -> 90 deg, 1 -> 0 deg)")]
            [Range(0.0F, 1.0F)]
            public float Accuracy;

            [Tooltip("투사체 공격력")]
            public float Damage;

            [Tooltip("발사음")]
            public string ShotSound;

            public bool Armed => Projectile != null && RPS > 0.0F;

            public float FireCooldown => Armed ? 1.0F / RPS : 0.0F;

            // 탄 퍼짐 정도(Radian)
            public float Spread => Armed ? (1.0F - Accuracy) * math.TORADIANS * 90.0F : 0.0F;
        }

        /// <summary>
        /// 순양함, 모선의 자동 포탑 칸에 장착했을 때의 사양. 정확도, RPS 는 <see cref="TurretSpec"/>을 따름.
        /// </summary>
        [Serializable]
        public struct AutoTurretSpec
        {
            [Tooltip("한 번에 사격하는 시간(초)")]
            [Min(0.0F)]
            public float FireDuration;

            [Tooltip("사격 후 휴식 시간 추출 범위(초). (최소, 최대)")]
            public Vector2 RestDuration;

            [Tooltip("적을 향해 도는 속도(도/초)")]
            [Min(0.0F)]
            public float TurningSpeed;

            [Tooltip("포탑이 돌 수 있는 부채꼴 각도(도). 가운데가 처음 방향.")]
            [Range(0.0F, 360.0F)]
            public float SightAngle;

            [Tooltip("적을 찾는 거리")]
            [Min(0.0F)]
            public float DetectRange;
        }

        [SerializeField]
        [Tooltip("우주선의 모든 총구에 적용하는 사양")]
        public TurretSpec Spec;

        [SerializeField]
        [Tooltip("자동 포탑으로 쓸 때의 사양")]
        public AutoTurretSpec AutoSpec = new AutoTurretSpec
        {
            FireDuration = 2.0F,
            RestDuration = new Vector2(1.0F, 2.0F),
            TurningSpeed = 180.0F,
            SightAngle   = 120.0F,
            DetectRange  = 20.0F,
        };

        [SerializeField]
        [Tooltip("사격 시작/중지 전환 시간(초). 우주선 단위. 전환 중에는 쏠 수 없음. 충전 포탑은 충전 시간.")]
        [Min(0.0F)]
        public float SwitchTime;

        [SerializeField]
        [Tooltip("충전 포탑. 누르고 있으면 SwitchTime 동안 충전하고, 다 찬 뒤 떼면 한 발 쏨. " +
                 "다 차기 전에 떼면 쏘지 않고 쿨다운만 돎. 쿨다운 중에는 충전하지 않음. 조종 포탑만.")]
        public bool ReleaseFire;

        [SerializeField]
        [Tooltip("사격 중 우주선 회전속도가 줄어드는 비율. 0 이면 그대로, 1 이면 회전 불가.")]
        [Range(0.0F, 1.0F)]
        public float ManeuverPenalty;

        [SerializeField]
        [Tooltip("쌓이는 열. PerShot 은 사격마다(총구 수 무관), PerSecond 는 사격 시작 전환 ~ 사격 중 초당. 플레이어만.")]
        [Min(0.0F)]
        public float Heat = 1.0F;

        [SerializeField]
        public TurretHeatMode HeatMode;

        [SerializeField]
        [Tooltip("사격마다 우주선을 발사 반대 방향으로 미는 속력. 총구마다 따로 밀림. 플레이어만.")]
        [Min(0.0F)]
        public float Recoil;

        [SerializeField]
        [Tooltip("사격을 시작할 때 한 번 재생. 충전 포탑은 충전음이고 떼면 줄어들며 멈춤. 비우면 없음.")]
        public string LaunchSound;

        [SerializeField]
        [Tooltip("사격 중 반복 재생. 지정하면 투사체마다의 발사음(ShotSound)은 재생하지 않음.")]
        public string LoopSound;

        [SerializeField]
        [Tooltip("충전 포탑만. 다 차기 전에 떼면 재생. 비우면 없음.")]
        public string CancelSound;

        [SerializeField]
        [Tooltip("투사체가 우주선에 맞을 때 재생. 쏜 플레이어에게는 화면 밖이어도 크게 들림. 비우면 없음.")]
        public string HitSound;

        [SerializeField]
        [Tooltip("명중음을 내는 시점. Predicted 는 클라이언트 판정으로 바로, Simulated 는 서버 판정을 받고 냄.")]
        public HitSoundMode HitSoundMode;
    }
}
