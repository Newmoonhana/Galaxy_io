using System;
using UnityEngine;

namespace OOPSample
{
    /// <summary>
    /// ECS 샘플의 <c>SimulationConfig</c> 싱글톤 컴포넌트에 대응하는 클래스입니다.
    ///
    /// DOTS에서는 이 데이터가 Unmanaged 싱글톤 엔티티로 베이킹되어 Burst 잡에서 읽히지만,
    /// OOP 버전에서는 그냥 <see cref="SimulationRunner"/>가 들고 있는 평범한 직렬화 객체입니다.
    /// 런타임에 HUD가 <see cref="MaxBotCount"/>를 바꾸면 <c>BotSpawner</c>가 다음 프레임에 반영합니다.
    /// </summary>
    [Serializable]
    public class SimulationConfig
    {
        [Header("Prefabs")]

        public BotBehaviour BotPrefab;
        public PlayerBehaviour PlayerPrefab;

        [Header("Population")]

        [Min(0)]
        public int MaxBotCount = 1000;

        [Tooltip("한 프레임에 Instantiate할 수 있는 봇의 최대 개수.")]
        [Min(1)]
        public int MaxSpawnsPerFrame = 1000;

        [Header("Bots")]

        [Min(0.01f)]
        public float BotSize = 12f;

        [Min(0f)]
        public float BotSpeedMin = 40f;

        [Min(0f)]
        public float BotSpeedMax = 200f;

        [Header("Player")]

        [Min(0.01f)]
        public float PlayerSize = 40f;

        [Min(0f)]
        public float PlayerSpeed = 600f;
    }
}
