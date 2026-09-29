using System;
using System.Collections.Generic;
using GalaxyIO.DOTS.Components.Singleton;
using UnityEngine;

namespace GalaxyIO.DOTS.Authoring.Config
{
    /// <summary>
    /// 런타임에 스폰할 프리팹 목록. <see cref="PrefabRegistryAuthoring"/>를 통해 베이킹됨.
    /// </summary>
    [CreateAssetMenu(fileName = "SO_PrefabRegistry", menuName = "GalaxyIO/Prefab Registry")]
    public sealed class PrefabRegistryAsset : ScriptableObject
    {
        [Serializable]
        public struct Entry
        {
            [Tooltip("코드와 네트워크가 이 프리팹을 가리키는 키")]
            public PrefabId Id;

            public GameObject Prefab;
        }

        [SerializeField]
        [Tooltip("PrefabId 로 찾을 프리팹 목록. 순서는 상관없음.")]
        public List<Entry> Prefabs = new List<Entry>();
    }
}
