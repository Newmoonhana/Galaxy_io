using System;
using System.Collections.Generic;
using GalaxyIO.DOTS.Components.Singleton;
using Unity.Collections;
using Unity.Entities;
using UnityEngine;

namespace GalaxyIO.DOTS.Authoring.Config
{
    /// <summary>
    /// 런타임에 스폰할 프리팹 목록. 월드당 한 개.
    /// </summary>
    public sealed class PrefabRegistryAuthoring : MonoBehaviour
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

    public sealed class PrefabRegistryBaker : Baker<PrefabRegistryAuthoring>
    {
        public override void Bake(PrefabRegistryAuthoring authoring)
        {
            Entity entity = GetEntity(TransformUsageFlags.None);
            DynamicBuffer<PrefabRegistryElement> registry = AddBuffer<PrefabRegistryElement>(entity);

            // 가장 큰 Id 까지 빈 칸으로 채워 둠. 칸 번호가 곧 Id.
            int maxId = 0;

            foreach (PrefabRegistryAuthoring.Entry entry in authoring.Prefabs)
            {
                maxId = Math.Max(maxId, (int)entry.Id);
            }

            registry.Resize(maxId + 1, NativeArrayOptions.ClearMemory);

            foreach (PrefabRegistryAuthoring.Entry entry in authoring.Prefabs)
            {
                if (entry.Id == PrefabId.None || entry.Prefab == null)
                {
                    Debug.LogWarning($"PrefabRegistry: 비어 있는 항목을 건너뜀. ({entry.Id})", authoring);
                    continue;
                }

                if (registry.Contains(entry.Id))
                {
                    Debug.LogWarning($"PrefabRegistry: Id 중복. 뒤의 항목을 버림. ({entry.Id})", authoring);
                    continue;
                }

                registry[(int)entry.Id] = new PrefabRegistryElement
                {
                    Prefab = GetEntity(entry.Prefab, TransformUsageFlags.Dynamic),
                };
            }
        }
    }
}