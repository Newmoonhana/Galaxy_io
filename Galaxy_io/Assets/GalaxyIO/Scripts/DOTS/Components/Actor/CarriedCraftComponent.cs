using GalaxyIO.DOTS.Authoring;
using GalaxyIO.DOTS.Components.Singleton;
using Unity.Entities;
using Unity.Mathematics;

namespace GalaxyIO.DOTS.Components.Actor
{
    /// <summary>
    /// <see cref="CarriedCraftAuthoring"/>의 DOTS 구조체. 함재기의 비행, 진형 사양.
    /// </summary>
    public struct CarriedCraftSpecComponent : IComponentData
    {
        // 비행 모드 최대 속력, 가속도, 감속도
        public float Speed;
        public float Acceleration;
        public float Deceleration;

        // 진형 자리와의 거리가 이보다 가까워지면 호버링, FlightDistance 보다 멀어지면 비행.
        public float HoverDistance;
        public float FlightDistance;

        // 호위 중에는 모선에서 이보다 먼 적을 쫓지 않음.
        public float LeashRadius;

        // 대상 선체 바깥이 이 거리 안이어야 쏨.
        public float FireRange;

        // 출격 후 쏘지 않는 시간(초). 모선 안에서 쏘지 않게 함.
        public float LaunchWindUp;

        // 진형을 벗어나 교전할 때 대상 선체 바깥과 두는 거리
        public float EngageDistance;

        // 공격 진형 첫 고리와 대상 선체 바깥 사이 거리
        public float AttackRadius;

        // 공격 진형 고리 수. 자리마다 이 중 하나를 무작위로 고름.
        public int AttackRings;

        // 지키기 진형 첫 고리 반지름
        public float SecureRadius;

        // 호위 진형 첫 고리와 모선 선체 바깥 사이 거리
        public float EscortRadius;

        // 고리 사이 거리
        public float RingGap;

        // 한 고리 위 자리 사이의 최소 거리
        public float SlotGap;

        // 공격 진형 자리가 고리를 따라 도는 속력
        public float OrbitSpeed;

        // 공격 진형 자리가 도는 방향을 바꾸는 간격(초). (최소, 최대)
        public float2 DodgeInterval;

        // 공격 중 체력 비율이 이보다 낮으면 모선으로 돌아감.
        public float RetreatHealth;
    }

    /// <summary>
    /// 출격한 함재기의 상태. 명령 정보는 <see cref="CarrierComponent"/>를 가진 모선이 매 틱 채움.
    /// </summary>
    public struct CarriedCraftComponent : IComponentData
    {
        // 출격시킨 모선
        public Entity Carrier;

        // 모선 주인의 NetworkId. 격추 기록에 씀.
        public int OwnerNetworkId;

        // 함재기 강화로 늘어난 피해 배율
        public float DamageScale;

        public CarrierOrder Order;

        // Attack 대상
        public Entity Target;

        // 진형 중심과 그 이동 속도. 귀환 중이면 모선.
        public float2 Anchor;
        public float2 AnchorVelocity;

        // 진형 중심의 기수. 호위 중이면 이 방향을 바라봄.
        public float2 Heading;

        // 이번 틱의 진형 자리(월드 좌표)
        public float2 Slot;

        // 모선으로 돌아가는 중. 닿으면 흡수됨.
        public bool Returning;

        // 귀환 명령 후 출발까지 남은 시간(초)
        public float ReturnRemaining;

        // 지키기, 호위 진형의 자리 번호. 없으면 -1.
        public int SlotIndex;

        // 공격 진형의 고리 번호. 없으면 -1.
        public int Ring;

        // 공격 진형 자리의 각도(라디안)와 도는 방향(1 반시계, -1 시계)
        public float Angle;
        public float Orbit;

        // 공격 진형 자리가 도는 방향을 바꾸기까지 남은 시간(초)
        public float DodgeRemaining;

        // 비행 모드인지. 아니면 호버링.
        public bool Flying;

        // 공격 명령의 접근 단계. 대상을 보고 쏘며 자기 고리까지 다가가고, 닿으면 진형을 짜고 돎.
        public bool Approaching;

        // 출격 후 쏘기 시작하기까지 남은 시간(초)
        public float WindUpRemaining;

        // 모선이 사라짐. ExplodeRemaining 뒤에 터짐.
        public bool Abandoned;
        public float ExplodeRemaining;
    }

    /// <summary>
    /// 함재기가 주운 재료 한 종류. 흡수되면 주인 인벤토리로, 격추되면 플레이어 사망 드랍 규칙대로 떨어뜨림.
    /// </summary>
    [InternalBufferCapacity(0)]
    public struct CarriedItemElement : IBufferElementData
    {
        public ItemId Id;
        public int Count;
    }

    /// <summary>
    /// 함재기와 모선 사이의 충돌 규칙.
    /// </summary>
    public static class CarriedCraft
    {
        /// <summary>
        /// 충돌 판정에서 뺄 상대. 함재기는 자기 모선과 부딪히지 않음.
        /// </summary>
        public static Entity IgnoredCarrier(in ComponentLookup<CarriedCraftComponent> crafts, Entity entity)
        {
            return crafts.TryGetComponent(entity, out CarriedCraftComponent craft) ? craft.Carrier : Entity.Null;
        }

        /// <summary>
        /// 같은 모선에 딸린 함재기끼리인지. 이 사이는 약하게만 밀어냄.
        /// </summary>
        public static bool SameGroup(in ComponentLookup<CarriedCraftComponent> crafts, Entity lhs, Entity rhs)
        {
            return crafts.TryGetComponent(lhs, out CarriedCraftComponent lhsState)
                   && crafts.TryGetComponent(rhs, out CarriedCraftComponent rhsState)
                   && lhsState.Carrier == rhsState.Carrier;
        }
    }
}
