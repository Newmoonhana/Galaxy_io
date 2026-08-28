namespace OOPSample
{
    /// <summary>
    /// <see cref="SimulationRunner"/>가 프레임당 한 번 게시하는 델타 타임.
    ///
    /// ECS 쪽 잡은 <c>SystemAPI.Time.DeltaTime</c>을 잡 필드로 한 번 복사받아 씁니다. 봇마다
    /// <c>UnityEngine.Time.deltaTime</c>(네이티브 프로퍼티 호출)을 때리게 두면 아키텍처가 아니라
    /// 엔진 API 호출 횟수를 비교하게 되므로, 여기서도 한 번만 읽어 공유합니다.
    /// </summary>
    public static class SimulationTime
    {
        public static float DeltaTime { get; internal set; }
    }
}
