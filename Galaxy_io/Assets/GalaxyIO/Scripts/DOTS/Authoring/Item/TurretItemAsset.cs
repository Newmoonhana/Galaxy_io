using System;
using System.Collections.Generic;
using GalaxyIO.DOTS.Components.Singleton;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Serialization;

namespace GalaxyIO.DOTS.Authoring
{
    /// <summary>
    /// 포탑 아이템. 인벤토리 포탑 칸에 넣으면 우주선 총구에 장착됨.
    /// </summary>
    [CreateAssetMenu(fileName = "SO_Item_Turret", menuName = "GalaxyIO/Turret Item")]
    public sealed class TurretItemAsset : ItemAsset
    {
        [Serializable]
        public struct TurretSpec
        {
            [Tooltip("발사체. 비우면 이 총구는 쏘지 않음.")]
            public GameObject Projectile;

            [Tooltip("초당 발사 수. 따라 쏘는 총구는 쓰지 않음.")]
            [Min(0.0F)]
            public float RPS;

            [Tooltip("따라 쏠 총구 번호(1부터). 0 이면 혼자 RPS 대로 쏨. 따라 쏘는 총구는 앞 총구가 쏠 때마다 " +
                     "FollowDelay 뒤에 한 번 쏨(방아쇠를 떼도 쏨). 앞 총구는 따라 쏘는 총구가 아니어야 함.")]
            [Min(0)]
            public int FollowMuzzle;

            [Tooltip("따라 쏘는 총구만. 앞 총구가 쏜 뒤 이 총구가 쏘기까지의 시간(초).")]
            [Min(0.0F)]
            [FormerlySerializedAs("FirstShotCooldown")]
            public float FollowDelay;

            [Tooltip("한 번에 쏘는 투사체 수. 각자 탄퍼짐, 속력 분포를 따로 받음. 0 은 1 로 취급.")]
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
            public AudioClip ShotSound;

            public bool Armed => Projectile != null && (RPS > 0.0F || FollowMuzzle > 0);

            public float FireCooldown => Armed && RPS > 0.0F ? 1.0F / RPS : 0.0F;

            // 탄 퍼짐 정도(Radian)
            public float Spread => Armed ? (1.0F - Accuracy) * math.TORADIANS * 90.0F : 0.0F;
        }

        [SerializeField]
        [Tooltip("우주선 총구 순서대로 적용. 우주선 총구보다 많은 항목은 무시함.")]
        public List<TurretSpec> TurretSpecs = new List<TurretSpec>();

        [SerializeField]
        [Tooltip("사격 시작/중지 전환 시간(초). 우주선 단위. 전환 중에는 쏠 수 없음.")]
        [Min(0.0F)]
        public float SwitchTime;

        [SerializeField]
        [Tooltip("사격을 시작할 때 한 번 재생. 비우면 없음.")]
        public AudioClip LaunchSound;

        [SerializeField]
        [Tooltip("사격 중 반복 재생. 지정하면 투사체마다의 발사음(ShotSound)은 재생하지 않음.")]
        public AudioClip LoopSound;

        private void Reset()
        {
            Type = ItemType.Turret;
            Stacking = 1;
        }
    }
}
