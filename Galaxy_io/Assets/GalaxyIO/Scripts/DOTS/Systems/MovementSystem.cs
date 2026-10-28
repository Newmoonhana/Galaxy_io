using GalaxyIO.DOTS.Components.Actor;
using Unity.Burst;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace GalaxyIO.DOTS.Systems
{
    /// <summary>
    /// <see cref="MovementComponent"/>의 속도와 회전량을 Transform 에 적분하는 시스템.
    /// 클라이언트가 직접 쏜 투사체는 <see cref="Client.Systems.LocalProjectileSystem"/>이 프레임마다 옮김.
    ///
    /// 이 코드는 DOTS 학습을 돕기 위하여 최대한 많은 주석을 원리와 함께 적어놓음.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(MovementSystemGroup))]
    public partial struct MovementSystem : ISystem
    {
        private EntityQuery query;

        /// <summary>
        /// 이 시스템이 DOTS 시뮬레이션에 의해 **시작될 때** 호출됨.
        /// <p/>
        /// 시스템이 호출되기 위한 전제 조건등을 설정함.
        /// </summary>
        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            // QueryBuilder의 대표적인 함수 종류
            //   - WithAll<T...>    나열한 컴포넌트를 "전부" 가진 Entity만 매칭. (AND)
            //   - WithAny<T...>    나열한 컴포넌트 중 "하나라도" 가지고 있으면 매칭. (OR)
            //   - WithNone<T...>   아예 안 가졌거나, 가졌어도 disable 되어있으면 매칭.
            //   - WithAbsent<T...> 아키타입에 아예 없어야 매칭. WithNone과 달리 가지고만 있으면 disable 되어있어도 탈락함.
            //
            // Simulate 는 모든 Entity 에 붙어 있고, 예측 루프에서는 이번 틱에 시뮬레이션할 Entity 만 켜져 있음.
            query = SystemAPI.QueryBuilder()
                .WithAllRW<LocalTransform>()
                .WithAll<MovementComponent, Simulate>()
                .WithNone<LocalProjectileTag>()
                .Build();

            // RequireForUpdate: 시스템이 호출되기 위한 사전 조건 설정
            //   - RequireForUpdate<T>() <T>     컴포넌트를 포함하는 Entity가 월드에 있을 때에 이 시스템을 호출함
            //   - RequireForUpdate(EntityQuery) 직접 작성한 쿼리에 맞는 Entity 가 있을 때에 호출함.
            state.RequireForUpdate(query);
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
            // ScheduleParallel: 병렬(Parallel)로 Job을 스케줄링함. 쿼리를 넘기면 그 쿼리의 Entity 만 순회함.
            state.Dependency = new MovementJob
            {
                DeltaTime = SystemAPI.Time.DeltaTime,
            }.ScheduleParallel(query, state.Dependency);
        }
    }

    /// <summary>
    /// 위치 이동과 Z 축 회전.
    /// </summary>
    [BurstCompile]
    public partial struct MovementJob : IJobEntity
    {
        public float DeltaTime;

        // Job의 작업 내용. 파라미터들은 DOTS 시스템이 자동으로 Entity에서 찾아줌.
        private void Execute(ref LocalTransform transform, in MovementComponent movement)
        {
            transform.Position = new float3(
                transform.Position.xy + movement.Velocity * DeltaTime,
                transform.Position.z
            );

            if (movement.AngularVelocity != 0.0F)
            {
                // 계속 곱하면 오차가 쌓이므로 정규화함.
                transform.Rotation = math.normalize(
                    math.mul(quaternion.RotateZ(movement.AngularVelocity * DeltaTime), transform.Rotation)
                );
            }
        }
    }
}
