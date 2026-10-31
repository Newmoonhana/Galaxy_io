using GalaxyIO.DOTS.Components.Singleton;
using GalaxyIO.DOTS.Systems;
using Unity.Entities;
using Unity.Mathematics;

namespace GalaxyIO.DOTS.Components.Actor
{
    public enum KingCarrierState : byte
    {
        // 적 대장선을 모름. 정찰대를 내보내며 이리저리 날아다님.
        Searching = 0,

        // 적 대장선 위치를 앎. 일정 거리까지 다가가고 그 위치로 편대를 보냄.
        Chasing = 1,

        // 적 대장선은 모르는데 공격받는 중. 한 방향으로 달아나며 호위 편대를 내보냄.
        Fleeing = 2,
    }

    /// <summary>
    /// 대장선 잡기의 대장선. AI 가 조종하는 모선. 서버만 가짐. <see cref="KingCarrierSystem"/>이 조종함.
    /// </summary>
    public struct KingCarrierComponent : IComponentData
    {
        public Team Team;

        public KingCarrierState State;

        // 지금 날아가는 방향
        public float2 Direction;

        // 탐색 중 방향을 바꾸기까지 남은 시간(초)
        public float CruiseRemaining;

        // 다음 편대를 내보내기까지 남은 시간(초)
        public float LaunchRemaining;

        // 마지막으로 알게 된 적 대장선 위치
        public float2 EnemyPosition;

        // 이 시각(ElapsedTime)까지 적 대장선 위치를 앎. 직접 찾거나 보고를 받으면 늘어남.
        public double KnownUntil;

        // 적 대장선을 직접 찾음. 아니면 보고만 받음.
        public bool EnemySighted;

        // 직전 틱의 체력과 마지막으로 맞은 시각. 쫓기는지 판단함.
        public float LastHealth;
        public double LastHitTime;

        // 맵 가장자리에서 안쪽으로 돌아오는 중. 다른 행동보다 먼저 함.
        public bool Returning;

        // 상대 팀 투사체에 마지막으로 맞은 시각(ElapsedTime). ProjectileDamageSystem 이 씀.
        public double LastEnemyHitTime;

        // 팀에 공격받는다고 마지막으로 알린 시각(ElapsedTime)
        public double LastAlertTime;

        // 직접 찾거나 함재기가 찾은 위치를 기억하는 시간(초)
        public const double SightMemory = 2.0;

        // 플레이어 핑으로 받은 위치를 기억하는 시간(초)
        public const double PingMemory = 30.0;
    }

    /// <summary>
    /// 대장선의 함재기 편대. <see cref="CarrierSystem"/>이 편대마다 명령을 따로 유지함.
    /// 편대 번호는 <see cref="CarriedCraftComponent.Squad"/>와 같음.
    /// </summary>
    [InternalBufferCapacity(5)]
    public struct KingSquadElement : IBufferElementData
    {
        public CarrierOrder Order;
        public Entity Target;
        public float2 Position;
        public bool Reassign;
        public int FormationCount;

        // 아직 내보내지 않은 함재기 수
        public int Pending;

        // 정찰대. 정찰 지점에 닿으면 돌아옴.
        public bool Scouting;

        // 출격한 시각(ElapsedTime). 작전 시간이 지나면 돌아옴.
        public double LaunchTime;
    }
}
