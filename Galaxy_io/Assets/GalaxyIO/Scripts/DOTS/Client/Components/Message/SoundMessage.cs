using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

namespace GalaxyIO.DOTS.Client.Components.Message
{
    /// <summary>
    /// 재생할 효과음 한 건. 효과음 플레이어가 읽고 제거함.
    /// </summary>
    public struct SoundMessage : IComponentData
    {
        public UnityObjectRef<AudioClip> Clip;

        // 소리가 난 위치. 카메라에서 멀수록 작게 들림.
        public float2 Position;
    }
}
