using UnityEngine;

namespace GalaxyIO.DOTS.Authoring.Config
{
    /// <summary>
    /// 런타임에 스폰할 프리팹 목록. <see cref="PrefabRegistryAuthoring"/>를 통해 베이킹됨.
    /// 우주선 기종은 <see cref="SpaceshipRegistryAsset"/>, 투사체는 포탑 아이템이 들고 있음.
    /// </summary>
    [CreateAssetMenu(fileName = "SO_PrefabRegistry", menuName = "GalaxyIO/Prefab Registry")]
    public sealed class PrefabRegistryAsset : ScriptableObject
    {
        [SerializeField]
        [Tooltip("우주선 파괴 폭발 연출")]
        public GameObject ExplosionEffect;

        [SerializeField]
        [Tooltip("주기적으로 스폰되는 Strafing AI 적")]
        public GameObject EnemyStrafingShip;

        [SerializeField]
        [Tooltip("월드에 떨어진 아이템. 모든 아이템 종류가 공유함.")]
        public GameObject ItemEntity;
    }
}
