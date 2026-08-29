using Unity.Entities;
using UnityEngine;

namespace GalaxyIO.Authoring
{
    public sealed class WeaponAuthoring : MonoBehaviour
    {
        [Header("Attributes")]

        // 공격력
        public float Damage;

        // 공격 속도
        public float FireRate;

        // 발사당 과열 정도
        public float HeatPerShot;
    }

    public sealed class WeaponBaker : Baker<WeaponAuthoring>
    {
        public override void Bake(WeaponAuthoring authoring)
        {

        }
    }
}