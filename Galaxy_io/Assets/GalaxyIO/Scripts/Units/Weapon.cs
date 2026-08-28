using UnityEngine;

namespace GalaxyIO.Scripts.Units
{
    public sealed class Weapon : MonoBehaviour
    {
        [Header("Attributes")]

        // 공격력
        public float Damage;

        // 공격 속도
        public float FireRate;

        // 엔진 과열 정도
        public float Heat;
    }
}