using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

namespace GalaxyIO.DOTS.Client.Components.Message
{
    /// <summary>
    /// 로컬 플레이어의 예측 명중음, 격추 폭발음 한 건. DamagePopupView 가 때린 플레이어의 명중음처럼
    /// 최소 음량을 지켜 재생하고 제거함.
    /// </summary>
    public struct AttackerHitSoundMessage : IComponentData
    {
        // Master Audio Sound Group 이름
        public FixedString64Bytes SoundKey;

        // 명중 지점
        public float2 Position;
    }
}
