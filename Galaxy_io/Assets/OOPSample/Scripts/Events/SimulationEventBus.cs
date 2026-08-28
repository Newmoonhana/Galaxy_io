using System;
using System.Collections.Generic;

namespace OOPSample
{
    // ---------------------------------------------------------------------------------
    //  MESSAGING / EVENTS IN CLASSIC OOP
    // ---------------------------------------------------------------------------------
    //  ECS 샘플은 "event entity" 패턴을 씁니다. 프로듀서가 페이로드 컴포넌트만 달린 엔티티를
    //  만들고, EntityCommandBufferSystem이 그것을 실체화하고, 컨슈머 시스템들이 쿼리로 집어
    //  가고, 클린업 시스템이 프레임 끝에 전부 파괴합니다.
    //
    //  OOP 버전에서 같은 성질(프로듀서는 컨슈머를 모르고, 컨슈머끼리도 서로를 모르며,
    //  메시지 수명은 정확히 한 프레임)을 유지하려면 아래 세 가지가 필요합니다.
    //
    //    1. Publish  - 프로듀서(BotBehaviour)가 채널에 메시지를 쌓는다. 여기서는 아무 일도
    //                  일어나지 않는다. C# event를 즉시 호출하는 방식과 달리, 프로듀서가
    //                  순회 중인 컬렉션을 컨슈머가 건드려서 터지는 사고를 원천 차단한다.
    //                  (BotDespawner가 Destroy를 호출하는 시점이 봇 Update 한복판이면 곤란하다.)
    //    2. Dispatch - SimulationRunner.LateUpdate가 프레임에 한 번 채널을 flush한다.
    //                  ECS의 EventCommandBufferSystem + EventConsumerSystemGroup 자리.
    //    3. Clear    - flush 직후 비운다. ECS의 EventCleanupSystem 자리. 메시지는 생성된
    //                  프레임에 처리되거나, 영원히 처리되지 않는다.
    //
    //  구독자는 메시지를 하나씩이 아니라 리스트째로 받습니다. ScoreKeeper처럼 개수만 필요한
    //  컨슈머가 델리게이트를 N번 호출하지 않아도 되고, ECS 컨슈머가 쿼리 결과를 통으로 받는
    //  모양과도 같습니다. 봇이 10만 개인 상황에서 이 차이는 그냥 넘길 수 없습니다.
    //
    //  주의: 구독은 해제해야 합니다. ECS의 이벤트 엔티티에는 없던 종류의 부담이며, 이것이
    //  메시지 배선을 정적 이벤트가 아니라 인스턴스 소유로 둔 이유입니다. Runner가 죽으면
    //  버스도 같이 죽고, 구독도 같이 사라집니다.
    // ---------------------------------------------------------------------------------

    /// <summary>메시지 타입 하나에 대응하는 채널. 한 프레임짜리 우편함.</summary>
    public sealed class EventChannel<T> where T : struct
    {
        private readonly List<T> m_Messages;

        public EventChannel(int capacity = 256)
        {
            m_Messages = new List<T>(capacity);
        }

        /// <summary>이 프레임에 쌓인 메시지를 통째로 받는 컨슈머들.</summary>
        public event Action<IReadOnlyList<T>> Consumed;

        public int Count => m_Messages.Count;

        public void Publish(in T message) => m_Messages.Add(message);

        public void Dispatch()
        {
            if (m_Messages.Count == 0)
                return;

            Consumed?.Invoke(m_Messages);
        }

        public void Clear() => m_Messages.Clear();
    }

    /// <summary>
    /// 시뮬레이션이 쓰는 모든 채널의 모음. <see cref="SimulationRunner"/>가 소유하며,
    /// 프로듀서와 컨슈머에게 생성 시점에 주입됩니다. 전역 static 이벤트를 쓰지 않는 이유는
    /// 플레이 모드를 나갔다 들어와도 구독이 남지 않게 하기 위해서입니다.
    /// </summary>
    public sealed class SimulationEventBus
    {
        public EventChannel<BotHitPlayerEvent> BotHitPlayer { get; } = new EventChannel<BotHitPlayerEvent>();

        /// <summary>ECS의 EventCommandBufferSystem + EventConsumerSystemGroup.</summary>
        public void DispatchAll()
        {
            BotHitPlayer.Dispatch();
        }

        /// <summary>ECS의 EventCleanupSystem. 메시지에 한 프레임 수명을 강제합니다.</summary>
        public void ClearAll()
        {
            BotHitPlayer.Clear();
        }
    }
}
