using GalaxyIO.DOTS.Components;
using Unity.Burst;
using Unity.Entities;
using Unity.Mathematics;
using Unity.NetCode;
using Unity.Transforms;

namespace GalaxyIO.DOTS.Systems
{
    /// <summary>
    /// <see cref="SpaceshipMovementComponent"/>의 전방 벡터(Forward)와 속력(Speed)을 바탕으로
    /// 우주선의 Transform에 반영하는 시스템.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(PredictedSimulationSystemGroup))]
    [UpdateAfter(typeof(PlayerMovementSystem))]
    public partial struct SpaceshipMovementSystem : ISystem
    {
        /// <summary>
        /// 이 시스템이 DOTS 시뮬레이션에 의해 **시작될 때** 호출됨.
        /// <p/>
        /// 시스템이 호출되기 위한 전제 조건등을 설정함.
        /// </summary>
        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            // RequireForUpdate: 시스템이 호출되기 위한 사전 조건 설정
            //   - RequireForUpdate<T>() <T>     컴포넌트를 포함하는 Entity가 월드에 있을 때에 이 시스템을 호출함
            //   - RequireForUpdate(EntityQuery) SystemAPI.QueryBuilder()로 추출할 Entity들의 조건을 명세한 쿼리를 직접 작성함.
            //
            // QueryBuilder의 대표적인 함수 종류
            //   - WithAll<T...>    나열한 컴포넌트를 "전부" 가진 Entity만 매칭. (AND)
            //   - WithAny<T...>    나열한 컴포넌트 중 "하나라도" 가지고 있으면 매칭. (OR)
            //   - WithNone<T...>   아예 안 가졌거나, 가졌어도 disable 되어있으면 매칭.
            //   - WithAbsent<T...> 아키타입에 아예 없어야 매칭. WithNone과 달리 가지고만 있으면 disable 되어있어도 탈락함.
            state.RequireForUpdate(SystemAPI.QueryBuilder().WithAll<LocalTransform, SpaceshipMovementComponent>().Build());

            // 위 코드는 아래와 같이 써도 무방하다.
            //
            // state.RequireForUpdate<LocalTransform>();
            // state.RequireForUpdate<SpaceshipMovementComponent>();
        }

        /// <summary>
        /// 시스템을 **매 프레임** Update할때마다 호출됨.
        /// OnCreate에서 지정한 전제 조건을 만족해야 한다.
        /// </summary>
        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            // 이 예제는 OnUpdate에서 Entity들을 직접 업데이트 하지 않고, 작업 리스트를
            // Job System을 통해 스케줄링만 함.
            //
            // state.Dependency에 델리게이트 체이닝 하듯이 새로운 Job을 등록하고 다시 대입함.
            //
            // 시스템 작업 리스트 = 내 작업 + 시스템 작업 리스트
            //
            // ScheduleParallel: 병렬(Parallel)로 Job을 스케줄링함.
            state.Dependency = new MoveJob
            {
                DeltaTime = SystemAPI.Time.DeltaTime,
            }.ScheduleParallel(state.Dependency);
        }
    }

    /// <summary>
    /// 우주선의 위치 이동과 기수 방향 회전.
    /// </summary>
    [BurstCompile]
    [WithAll(typeof(SpaceshipMovementComponent), typeof(Simulate))]
    public partial struct MoveJob : IJobEntity
    {
        public float DeltaTime;

        // Job의 작업 내용. 파라미터들은 DOTS 시스템이 자동으로 Entity에서 찾아줌.
        //
        // NOTE: 시스템이 안전하게 해당 파라미터들을 찾을 수 있는 이유는 `OnCreate`에서 해당
        // 컴포넌트를 가지는 Entity만 순회하도록 `RequireForUpdate` 통해 강제해줬기 때문.
        private void Execute(ref LocalTransform transform, ref SpaceshipMovementComponent spaceshipSpaceshipMovement)
        {
            // Forward를 단위 벡터로 만들면서 Speed를 곱하는 스케일
            float len = math.length(spaceshipSpaceshipMovement.Forward);
            float2 Velocity = spaceshipSpaceshipMovement.Forward * (spaceshipSpaceshipMovement.Speed / len);

            transform.Position = new float3(transform.Position.xy + Velocity * DeltaTime, transform.Position.z);

            // 방향 벡터의 길이가 0인건 예외 케이스. 아무것도 하지 않음.
            if (len <= math.EPSILON) return;

            transform.Rotation = quaternion.RotateZ(
                -math.atan2(spaceshipSpaceshipMovement.Forward.x, spaceshipSpaceshipMovement.Forward.y)
            );
        }
    }
}