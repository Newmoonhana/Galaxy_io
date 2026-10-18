using GalaxyIO.DOTS.Authoring;
using Unity.Entities;
using Unity.Mathematics;
using Unity.NetCode;

namespace GalaxyIO.DOTS.Components.Actor
{
    /// <summary>
    /// 모선이 함재기에 내리는 명령.
    /// </summary>
    public enum CarrierOrder : byte
    {
        // 명령 없음. 함재기는 모두 모선 안에 있음.
        None = 0,

        // 대상을 둘러싸고 공격
        Attack = 1,

        // 지점으로 가며 만나는 적과 교전하고, 지점에 모여 지킴
        HoldPosition = 2,

        // 모선 주변을 지킴
        Escort = 3,

        // 모선으로 돌아와 흡수됨
        Return = 4,
    }

    /// <summary>
    /// <see cref="CarrierAuthoring"/>의 DOTS 구조체. 모선의 함재기 생산, 출격, 명령 상태.
    /// 명령과 함재기 수는 주인에게만 복제함.
    /// </summary>
    [GhostComponent(OwnerSendType = SendToOwnerType.SendToOwner)]
    public struct CarrierComponent : IComponentData
    {
        // 출격시킬 함재기 프리팹
        public Entity CraftPrefab;

        // 함재기 최대 수. 모선 안과 밖을 합친 수.
        public int Capacity;

        // 함재기 한 대를 만드는 시간(초). 강화 전 값.
        public float AssemblyTime;

        // 함재기를 한 대씩 내보내는 간격(초)
        public float LaunchInterval;

        // 명령을 다시 내릴 수 있기까지의 시간(초)
        public float OrderCooldown;

        // 귀환 명령을 받은 함재기가 출발하기까지 기다리는 최대 시간(초)
        public float ReturnDelay;

        [GhostField]
        public CarrierOrder Order;

        // Attack 대상. 클라이언트에 없는 Ghost 면 Entity.Null 로 받음.
        [GhostField]
        public Entity Target;

        // 명령 지점. Attack 은 대상의 현재 위치, Escort 는 모선 위치.
        [GhostField(Quantization = 100)]
        public float2 Position;

        // 모선 안에 있는 함재기 수
        [GhostField]
        public byte Stored;

        // 모선 안과 밖을 합친 함재기 수
        [GhostField]
        public byte Total;

        // 만들고 있는 함재기의 진행률(0 ~ 1). 최대 수면 1.
        [GhostField(Quantization = 100)]
        public float AssemblyProgress;

        // 다음 함재기가 완성되기까지 남은 시간(초)
        public float AssemblyRemaining;

        // 다음 함재기를 내보내기까지 남은 시간(초)
        public float LaunchRemaining;

        // 다음 명령을 받기까지 남은 시간(초)
        public float OrderRemaining;

        // 진형 자리를 계산한 함재기 수. 바뀌면 자리를 다시 나눔.
        public int FormationCount;

        // 새 명령을 받아 진형 자리를 다시 나눠야 함.
        public bool Reassign;
    }
}
