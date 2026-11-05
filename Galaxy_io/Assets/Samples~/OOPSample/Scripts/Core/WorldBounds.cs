using System;
using UnityEngine;

namespace OOPSample
{
    /// <summary>
    /// ECS 샘플의 <c>WorldBounds</c> 대응. 모든 개체가 갇혀 있는 사각형입니다.
    /// 봇마다 값 복사로 전달되므로 struct로 둡니다.
    /// </summary>
    [Serializable]
    public struct WorldBounds
    {
        public Vector2 Min;
        public Vector2 Max;

        public WorldBounds(Vector2 min, Vector2 max)
        {
            Min = min;
            Max = max;
        }
    }
}
