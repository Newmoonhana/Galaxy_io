using System;
using System.Collections.Generic;
using GalaxyIO.DOTS.Components.Singleton;
using UnityEngine;

namespace GalaxyIO.Scripts.Managed.Effects
{
    /// <summary>
    /// 기종별 엔진(노즐) 자리. 엔진 불빛, 가속 불꽃, 불똥이 여기서 나옴.
    /// 좌표는 우주선 기준 월드 단위(+y 가 기수). 에디터 메뉴 GalaxyIO/VFX/엔진 위치 측정 으로
    /// 각 기종 스프라이트의 꼬리 쪽 불투명 픽셀 덩어리를 재서 채움. 손으로 고쳐도 됨.
    /// </summary>
    [CreateAssetMenu(fileName = "SO_SpaceshipEngineLayout", menuName = "GalaxyIO/VFX/Spaceship Engine Layout")]
    public sealed class SpaceshipEngineLayout : ScriptableObject
    {
        [Serializable]
        public struct Engine
        {
            [Tooltip("우주선 기준 위치(월드 단위). +y 가 기수 방향.")]
            public Vector2 Position;

            [Tooltip("노즐 반지름(월드 단위). 불빛, 불꽃 크기의 기준.")]
            [Min(0.0F)]
            public float Radius;
        }

        [Serializable]
        public sealed class Entry
        {
            public SpaceshipId Ship;
            public List<Engine> Engines = new List<Engine>();
        }

        [SerializeField]
        private List<Entry> entries = new List<Entry>();

        public List<Entry> Entries => entries;

        public bool TryGet(SpaceshipId ship, out List<Engine> engines)
        {
            foreach (Entry entry in entries)
            {
                if (entry.Ship == ship)
                {
                    engines = entry.Engines;
                    return engines.Count > 0;
                }
            }

            engines = null;
            return false;
        }
    }
}
