using Unity.Mathematics;

namespace GalaxyIO.DOTS.Client.Systems
{
    /// <summary>
    /// 효과음 메시지를 만들기 전 가청 범위를 판정함.
    /// <para/>
    /// 반경은 Sound Group 의 3D maxDistance 와 같아야 함. 3D 그룹은 Linear 롤오프 min 5 / max 35 라
    /// 35 를 넘으면 음량이 정확히 0 이고, 한 번 재생한 one-shot 은 위치를 따라가지 않으므로
    /// 그 구간의 소리는 평생 묵음인 채로 음성 풀만 차지함(실측: 반경 40 일 때 재생된 Shot_* 음성의
    /// 86% 가 35~40 구간이었음).
    /// </summary>
    public static class SoundAudibleRange
    {
        public const float Radius = 75.0F;

        private const float RadiusSq = Radius * Radius;

        /// <summary>
        /// 들릴 수 있는 위치인지. 리스너 위치를 한 번도 못 잡았으면 들리지 않는 것으로 봄.
        /// </summary>
        public static bool IsAudible(bool _hasListener, float2 _listener, float2 _position)
        {
            return _hasListener && math.distancesq(_listener, _position) <= RadiusSq;
        }
    }
}
