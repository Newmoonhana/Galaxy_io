using Unity.Collections;
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
        public FixedString64Bytes PickupSound;

        // 플레이어가 파괴될 때 인벤토리 칸마다 그 칸을 떨어뜨릴 확률. 서버만 씀.
        public float PlayerSlotDropChance;

        // 떨어뜨릴 칸의 아이템 하나하나가 실제로 떨어질 확률. 서버만 씀.
        public float PlayerStackDropChance;

        // 플레이어 인벤토리 칸 수
        public int CommonSlots;
        public int TurretSlots;
        public int BombSlots;

        // 마지막 발사 후 이 시간(초)이 지나면 포탑 열이 식기 시작함.
        public float NoFireStandbyTime;

        // 최대 열에서 0 까지 식는 시간(초). 평소, 과열 시.
        public float CoolingTime;
        public float OverheatCoolingTime;
    }
}
