using GalaxyIO.DOTS.Components.Actor;
using Unity.Burst;
using Unity.Entities;
using Unity.Mathematics;
using Unity.NetCode;
using Unity.Transforms;

namespace GalaxyIO.DOTS.Systems
{
    /// <summary>
    /// 아이템을 스폰 때 정해진 방향으로 천천히 돌리며 떠다니게 하고, 감지 원 안의 플레이어에게 끌어당기는 시스템.
    /// </summary>
    [BurstCompile]
    [WorldSystemFilter(WorldSystemFilterFlags.ServerSimulation)]
    [UpdateInGroup(typeof(MovementSystemGroup))]
    public partial struct ItemMovementSystem : ISystem
    {
        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<ItemComponent>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            state.Dependency = new ItemMovementJob
            {
                DeltaTime = SystemAPI.Time.DeltaTime,
            }.ScheduleParallel(state.Dependency);
        }
    }

    /// <summary>
    /// 처음 감지한 플레이어를 계속 따라감. 그 플레이어가 원을 벗어나면 버퍼 맨 앞 플레이어로 바꿈.
    /// 따라갈 대상이 없으면 떠다니는 속력까지 미끄러진 뒤 계속 떠다님.
    /// </summary>
    [BurstCompile]
    public partial struct ItemMovementJob : IJobEntity
    {
        public float DeltaTime;

        private void Execute(
            ref LocalTransform transform,
            ref ItemComponent item,
            in DynamicBuffer<ItemOverlapElement> overlaps)
        {
            // 오른쪽으로 떠가면 시계 방향, 왼쪽이면 반시계 방향으로 돎.
            transform = transform.RotateZ(-math.sign(item.Velocity.x) * item.SpinSpeed * DeltaTime);

            int index = -1;

            // 줍기 대기 중에는 끌려가지 않고 미끄러지기만 함.
            for (int i = 0; i < overlaps.Length && item.PickupDelay <= 0.0F; i++)
            {
                if (overlaps[i].Message.Other == item.Target)
                {
                    index = i;
                    break;
                }
            }

            if (index < 0 && overlaps.Length > 0 && item.PickupDelay <= 0.0F)
            {
                index = 0;
            }

            item.Target = index < 0 ? Entity.Null : overlaps[index].Message.Other;

            float2 displacement;

            if (index < 0)
            {
                float speed = math.length(item.Velocity);

                if (speed <= 0.0F)
                {
                    return;
                }

                displacement = item.Velocity * DeltaTime;

                // 떠다니는 속력까지만 느려지고 그 뒤로는 같은 방향으로 계속 떠다님.
                item.Velocity *= math.max(speed - item.SlideDeceleration * DeltaTime, item.FloatSpeed) / speed;
            }
            else
            {
                // 끌려가기 시작하면 미끄러짐은 끝남. 대상을 놓치면 원래 방향으로 다시 떠다님.
                item.Velocity = math.normalizesafe(item.Velocity) * item.FloatSpeed;

                // 도착 지점을 넘어가지 않게 남은 거리에서 멈춤.
                float2 toTarget = overlaps[index].Message.OtherCenter - transform.Position.xy;
                float distance = math.length(toTarget);
                float step = item.FollowSpeed * DeltaTime;

                displacement = distance <= step ? toTarget : toTarget / distance * step;
            }

            transform.Position.xy += displacement;
        }
    }
}
