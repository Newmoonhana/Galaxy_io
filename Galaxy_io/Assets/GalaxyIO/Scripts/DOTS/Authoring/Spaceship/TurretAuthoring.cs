using UnityEngine;

namespace GalaxyIO.DOTS.Authoring
{
    /// <summary>
    /// 터렛 속성 정의. Baking은 <see cref="SpaceshipBaker"/>가 담당함.
    /// </summary>
    public sealed class TurretAuthoring : MonoBehaviour
    {
        [SerializeField]
        [Tooltip("발사체")]
        public GameObject Projectile;

        [SerializeField]
        [Tooltip("초당 발사 수")]
        public uint RPS;

        [SerializeField]
        [Tooltip("정확도 (0 -> 90 deg, 1 -> 0 deg)")]
        [Range(0.0F, 1.0F)]
        public float Accuracy;

        [SerializeField]
        [Tooltip("발사음. 한 번의 발사에서 같은 소리는 한 번만 재생됨.")]
        public AudioClip ShotSound;
    }
}