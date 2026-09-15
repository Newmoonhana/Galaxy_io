using GalaxyIO.DOTS.Authoring;
using GalaxyIO.DOTS.Systems;
using Unity.Entities;
using Unity.Mathematics;

namespace GalaxyIO.DOTS.Components.Actor
{
    /// <summary>
    /// <see cref="EnemyAuthoring"/>의 DOTS 구조체. 모든 AI 가 공유하는 인지 정보.
    /// <see cref="EnemyPerceptionSystem"/>이 매 틱 갱신함.
    /// </summary>
    public struct EnemyPerceptionComponent : IComponentData
    {
        // 현재 공격 대상. 없으면 Entity.Null.
        public Entity CurrentTarget;

        // CurrentTarget 의 이번 틱 위치. 대상이 없으면 의미 없음.
        public float2 TargetPosition;

        // 부채꼴 시야. 이 충돌체에 겹친 적대 진영 우주선을 대상으로 삼음.
        public ColliderComponent Sight;

        // 대상과의 거리가 이보다 멀어지면 대상을 놓침.
        public float ChaseDistance;

        // 새 대상으로 삼을 진영 마스크. FactionID 의 OR.
        public uint HostileFactions;
    }
}