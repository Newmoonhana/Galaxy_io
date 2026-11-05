using Unity.Burst;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace GalaxyIO
{
    /// <summary>
    /// Integrates every bot and reflects it off the world bounds. Runs as a parallel
    /// Burst job across all chunks, which is what makes thousands of boxes essentially free.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(MovementSystemGroup))]
    public partial struct BotMovementSystem : ISystem
    {
        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<WorldBounds>();
            state.RequireForUpdate(SystemAPI.QueryBuilder().WithAll<BotTag>().Build());
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            state.Dependency = new BotMoveJob
            {
                DeltaTime = SystemAPI.Time.DeltaTime,
                Bounds = SystemAPI.GetSingleton<WorldBounds>()
            }.ScheduleParallel(state.Dependency);
        }
    }

    [BurstCompile]
    [WithAll(typeof(BotTag))]
    public partial struct BotMoveJob : IJobEntity
    {
        public float DeltaTime;
        public WorldBounds Bounds;

        void Execute(ref LocalTransform transform, ref Velocity velocity, in BoxSize size)
        {
            var half = size.Value * 0.5f;
            var min = Bounds.Min + half;
            var max = Bounds.Max - half;

            var position = transform.Position.xy + velocity.Value * DeltaTime;

            // Elastic bounce off the walls. The direction chosen at spawn is preserved
            // except for the sign flip a wall imposes, so nothing ever leaves the world.
            if (position.x < min.x)
            {
                position.x = min.x + (min.x - position.x);
                velocity.Value.x = -velocity.Value.x;
            }
            else if (position.x > max.x)
            {
                position.x = max.x - (position.x - max.x);
                velocity.Value.x = -velocity.Value.x;
            }

            if (position.y < min.y)
            {
                position.y = min.y + (min.y - position.y);
                velocity.Value.y = -velocity.Value.y;
            }
            else if (position.y > max.y)
            {
                position.y = max.y - (position.y - max.y);
                velocity.Value.y = -velocity.Value.y;
            }

            position = math.clamp(position, min, max);
            transform.Position = new float3(position, transform.Position.z);
        }
    }

    /// <summary>
    /// Moves the player using the <see cref="PlayerInputState"/> singleton that
    /// <c>PlayerInputSystem</c> published earlier in the frame, then clamps to the bounds.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(MovementSystemGroup))]
    public partial struct PlayerMovementSystem : ISystem
    {
        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<SimulationConfig>();
            state.RequireForUpdate<WorldBounds>();
            state.RequireForUpdate<PlayerInputState>();
            state.RequireForUpdate(SystemAPI.QueryBuilder().WithAll<PlayerTag>().Build());
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var config = SystemAPI.GetSingleton<SimulationConfig>();
            var input = SystemAPI.GetSingleton<PlayerInputState>();

            state.Dependency = new PlayerMoveJob
            {
                DeltaTime = SystemAPI.Time.DeltaTime,
                Bounds = SystemAPI.GetSingleton<WorldBounds>(),
                Move = input.Move,
                Speed = config.PlayerSpeed
            }.Schedule(state.Dependency);
        }
    }

    [BurstCompile]
    [WithAll(typeof(PlayerTag))]
    public partial struct PlayerMoveJob : IJobEntity
    {
        public float DeltaTime;
        public WorldBounds Bounds;
        public float2 Move;
        public float Speed;

        void Execute(ref LocalTransform transform, ref Velocity velocity, in BoxSize size)
        {
            var direction = math.lengthsq(Move) > 1e-6f ? math.normalize(Move) : float2.zero;
            velocity.Value = direction * Speed;

            var half = size.Value * 0.5f;
            var position = transform.Position.xy + velocity.Value * DeltaTime;
            position = math.clamp(position, Bounds.Min + half, Bounds.Max - half);

            transform.Position = new float3(position, transform.Position.z);
        }
    }
}