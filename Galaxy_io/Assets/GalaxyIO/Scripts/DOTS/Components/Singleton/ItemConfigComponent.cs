using GalaxyIO.DOTS.Authoring.Config;
using Unity.Entities;
using UnityEngine;

namespace GalaxyIO.DOTS.Components.Singleton
{
    /// <summary>
    /// <see cref="ItemConfigAuthoring"/>의 DOTS 구조체. 월드당 한 개.
    /// </summary>
    public struct ItemConfigComponent : IComponentData
    {
        // 아이템을 주웠을 때 재생할 효과음. 클라이언트만 씀.
        public UnityObjectRef<AudioClip> PickupSound;
    }
}
