using GalaxyIO.DOTS.Components.Actor;
using Unity.Burst;
using Unity.Entities;
using Unity.Mathematics;
using Unity.NetCode;
using Unity.Transforms;

namespace GalaxyIO.DOTS.Systems
{
    /// <summary>
    /// 스폰 직후 아이템을 미끄러뜨리며 튕기고, 감지 원 안의 플레이어에게 끌어당기는 시스템.
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
    /// 따라갈 대상이 없으면 남은 속도로 미끄러짐. 스폰 직후 미끄러지는 쪽으로 기울었다가 0도로 돌아옴.
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
            if (item.BounceRemaining > 0.0F)
            {
                item.BounceRemaining = math.max(item.BounceRemaining - DeltaTime, 0.0F);

                // sin 반 주기. 0도 -> 최대 각도 -> 0도.
                float progress = 1.0F - item.BounceRemaining / item.BounceDuration;
                transform.Rotation = quaternion.RotateZ(item.BounceSide * item.BounceAngle * math.sin(math.PI * progress));
            }

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
                item.Velocity *= math.max(speed - item.SlideDeceleration * DeltaTime, 0.0F) / speed;
            }
            else
            {
                // 끌려가기 시작하면 미끄러짐은 끝남. 도착 지점을 넘어가지 않게 남은 거리에서 멈춤.
                item.Velocity = float2.zero;

                float2 toTarget = overlaps[index].Message.OtherCenter - transform.Position.xy;
                float distance = math.length(toTarget);
                float step = item.FollowSpeed * DeltaTime;

                displacement = distance <= step ? toTarget : toTarget / distance * step;
            }

            transform.Position.xy += displacement;
        }
    }
}
