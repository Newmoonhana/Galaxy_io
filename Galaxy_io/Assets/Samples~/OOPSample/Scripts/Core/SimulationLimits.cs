using System;
using UnityEngine;

namespace OOPSample
{
    /// <summary>ECS 샘플의 <c>SimulationLimits</c> 대응. HUD 입력값을 clamp하는 상한선입니다.</summary>
    [Serializable]
    public class SimulationLimits
    {
        [Min(1)]
        public int HardMaxBotCount = 200000;
    }
}
