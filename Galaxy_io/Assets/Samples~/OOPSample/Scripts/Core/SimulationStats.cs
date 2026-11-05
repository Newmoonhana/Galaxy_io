namespace OOPSample
{
    /// <summary>
    /// ECS 샘플의 <c>SimulationStats</c> 대응. HUD가 읽는 읽기 전용 거울입니다.
    /// DOTS 쪽은 EntityQuery로 싱글톤을 뽑아 와야 했지만, 여기서는 그냥 참조를 넘기면 됩니다.
    /// </summary>
    public sealed class SimulationStats
    {
        public int AliveBotCount;
        public int DestroyedBotCount;
        public int DestroyedThisFrame;
    }
}
