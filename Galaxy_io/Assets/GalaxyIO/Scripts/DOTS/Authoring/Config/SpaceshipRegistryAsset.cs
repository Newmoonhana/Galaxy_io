using System.Collections.Generic;
using GalaxyIO.DOTS.Components.Singleton;
using UnityEngine;

namespace GalaxyIO.DOTS.Authoring.Config
{
    /// <summary>
    /// 상점에서 살 수 있는 우주선 기종 목록. <see cref="PrefabRegistryAuthoring"/>를 통해 베이킹됨.
    /// </summary>
    [CreateAssetMenu(fileName = "SO_SpaceshipRegistry", menuName = "GalaxyIO/Spaceship Registry")]
    public sealed class SpaceshipRegistryAsset : ScriptableObject
    {
        [SerializeField]
        [Tooltip("분류 안에서 이 순서대로 보여줌.")]
        public List<SpaceshipAsset> Spaceships = new List<SpaceshipAsset>();

        /// <summary>
        /// 등록된 기종. 없으면 null.
        /// </summary>
        public SpaceshipAsset Find(SpaceshipId id)
        {
            foreach (SpaceshipAsset spaceship in Spaceships)
            {
                if (spaceship != null && spaceship.Id != SpaceshipId.None && spaceship.Id == id)
                {
                    return spaceship;
                }
            }

            return null;
        }
    }
}
