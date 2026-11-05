using Unity.Entities;

namespace GalaxyIO
{
    /// <summary>싱글톤. 런타임에 HUD로 바뀔 수 있음.</summary>
    public struct SimulationConfig : IComponentData
    {
        /// <summary>Bot Prefab의 DOTS 버전. <c>BotSpawnSystem</c>에 의해 인스턴스화 됨.</summary>
        public Entity BotPrefab;

        /// <summary>Player prefab의 DOTS 버전. <c>PlayerSpawnSystem</c>에 의해 인스턴스화 됨.</summary>
        public Entity PlayerPrefab;

        /// <summary>개체수 제한. HUD의 Input Field를 통해 .</summary>
        public int MaxBotCount;

        /// <summary>1프레임당 생성될 수 있는 개체수 제한.</summary>
        public int MaxSpawnsPerFrame;

        public float BotSize;
        public float BotSpeedMin;
        public float BotSpeedMax;

        public float PlayerSize;
        public float PlayerSpeed;
    }
}