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

        // 플레이어가 파괴될 때 인벤토리 칸마다 그 칸을 떨어뜨릴 확률. 서버만 씀.
        public float PlayerSlotDropChance;

        // 떨어뜨릴 칸의 아이템 하나하나가 실제로 떨어질 확률. 서버만 씀.
        public float PlayerStackDropChance;

        // 플레이어 인벤토리 칸 수
        public int CommonSlots;
        public int TurretSlots;
        public int BombSlots;

        // 이 시간(초) 동안 쏘지 않으면 모든 총구가 첫 발 대기로 돌아감.
        public float NoFireStandbyTime;
    }
}
