using System;
using System.Collections.Generic;
using UnityEngine;

namespace GalaxyIO.DOTS.Authoring
{
    /// <summary>
    /// 파괴 시 떨어뜨릴 아이템 목록. <see cref="DropTableAuthoring"/>를 통해 베이킹됨.
    /// </summary>
    [CreateAssetMenu(fileName = "SO_DropTable", menuName = "GalaxyIO/Drop Table")]
    public sealed class DropTableAsset : ScriptableObject
    {
        [Serializable]
        public struct Entry
        {
            public ItemAsset Item;

            [Tooltip("한 번에 떨어뜨릴 개수 범위 (최소, 최대)")]
            public Vector2Int CountRange;

            [Tooltip("같은 Roll 안에서 뽑힐 비중")]
            [Min(0)]
            public int Weight;
        }

        [Serializable]
        public struct Roll
        {
            [Tooltip("Roll 성공 확률. 성공하면 항목 중 하나를 가중치로 고름.")]
            [Range(0.0F, 1.0F)]
            public float Chance;

            public List<Entry> Entries;
        }

        [SerializeField]
        [Tooltip("Roll 마다 따로 굴림.")]
        public List<Roll> Rolls = new List<Roll>();
    }
}
