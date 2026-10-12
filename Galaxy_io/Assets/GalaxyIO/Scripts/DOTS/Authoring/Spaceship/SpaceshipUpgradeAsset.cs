using System;
using System.Collections.Generic;
using GalaxyIO.DOTS.Components.Actor;
using UnityEngine;

namespace GalaxyIO.DOTS.Authoring
{
    /// <summary>
    /// 우주선 기종 하나의 강화 사양. <see cref="SpaceshipAuthoring"/>이 참조함.
    /// </summary>
    [CreateAssetMenu(fileName = "SO_Upgrade", menuName = "GalaxyIO/Spaceship Upgrade")]
    public sealed class SpaceshipUpgradeAsset : ScriptableObject
    {
        [Serializable]
        public struct Entry
        {
            public UpgradeStat Stat;

            [Tooltip("한 단계마다 더해지는 값. Damage, RadarSweep 는 배율(0.1 = 10%)")]
            public float Amount;

            [Range(0, 255)]
            public int MaxLevel;
        }

        [SerializeField]
        [Tooltip("능력치별 강화 사양. 빠진 능력치는 강화할 수 없음.")]
        public List<Entry> Entries = new List<Entry>();
    }
}
