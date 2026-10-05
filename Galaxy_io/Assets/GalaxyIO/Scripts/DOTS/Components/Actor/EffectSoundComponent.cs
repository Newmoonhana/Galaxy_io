using Unity.Collections;
using GalaxyIO.DOTS.Authoring;
using Unity.Entities;

namespace GalaxyIO.DOTS.Components.Actor
{
    /// <summary>
    /// <see cref="EffectAuthoring"/>의 DOTS 구조체. 연출과 함께 재생할 효과음.
    /// </summary>
    public struct EffectSoundComponent : IComponentData
    {
        //Master Audio Sound Group 이름
        public FixedString64Bytes SoundKey;
    }
}
