using Unity.Entities;

namespace GalaxyIO.DOTS.Components.Actor
{
    /// <summary>
    /// 충돌음 재전송 대기 시간. 맞붙어 비비는 동안 매 틱 충돌음이 나는 것 방지. 서버에서만 씀.
    /// </summary>
    public struct CrashSoundCooldownComponent : IComponentData
    {
        // 남은 시간(초). 0 이하면 다음 충돌에 충돌음을 보냄.
        public float Remaining;
    }
}
