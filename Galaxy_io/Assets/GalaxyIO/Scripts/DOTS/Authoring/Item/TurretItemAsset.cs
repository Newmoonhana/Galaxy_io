using System;
using System.Collections.Generic;
using GalaxyIO.DOTS.Components.Singleton;
using Unity.Mathematics;
using UnityEngine;

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

            [Tooltip("초당 발사 수")]
            public uint RPS;

            [Tooltip("정확도 (0 -> 90 deg, 1 -> 0 deg)")]
            [Range(0.0F, 1.0F)]
            public float Accuracy;

            [Tooltip("투사체 공격력")]
            public float Damage;

            [Tooltip("발사음")]
            public AudioClip ShotSound;

            public bool Armed => Projectile != null && RPS > 0;

            public float FireCooldown => Armed ? 1.0F / RPS : 0.0F;

            // 탄 퍼짐 정도(Radian)
            public float Spread => Armed ? (1.0F - Accuracy) * math.TORADIANS * 90.0F : 0.0F;
        }

        [SerializeField]
        [Tooltip("우주선 총구 순서대로 적용. 우주선 총구보다 많은 항목은 무시함.")]
        public List<TurretSpec> TurretSpecs = new List<TurretSpec>();

        private void Reset()
        {
            Type = ItemType.Turret;
            Stacking = 1;
        }
    }
}
