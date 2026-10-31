using GalaxyIO.DOTS.Collision;
using GalaxyIO.DOTS.Components.Actor;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace GalaxyIO.DOTS.Spaceship
{
    /// <summary>
    /// 길을 막는 선체. 함재기를 뺀 모든 우주선(아군 포함). 피하는 쪽 두께의 <see cref="PathAvoidance.NegligibleRatio"/>
    /// 미만인 선체는 무시하고 그대로 지나감(e.g. 모선 앞의 Corsair). 틱마다 <see cref="PathAvoidance.Collect"/>로 모음.
    /// </summary>
    public struct PathObstacle
    {
        public Entity Entity;

        // 충돌체 월드 중심
        public float2 Center;

        // 충돌체를 감싸는 원의 반지름
        public float Radius;

        // 선체 두께(SpaceshipSpecComponent.Thickness). 피하는 쪽보다 아주 작으면 무시함.
        public float Thickness;

        // 플레이어 우주선. 대장선 함재기는 플레이어 우주선과 부딪히지 않으므로 피하지 않음.
        public bool Player;
    }

    /// <summary>
    /// AI 길찾기의 장애물 회피. 가려는 방향의 직선 길이 선체(+ 여유)를 지나면, 가장 먼저 닿을 선체의 바깥 접선 쪽으로 비켜 감.
    /// 함재기, 대장선, AI 적이 함께 씀. 함재기는 같은 계산을 자기 루프에서 직접 함(CarriedCraftBehaviorJob).
    /// </summary>
    public static class PathAvoidance
    {
        /// <summary>
        /// 내 두께 대비 이 비율보다 얇은 선체는 피하지 않음. 모선(20) 기준 5 미만(전투기, Corsair)은 무시, 순양함(10)은 피함.
        /// </summary>
        public const float NegligibleRatio = 0.25F;

        /// <summary>
        /// 내 두께에 비해 무시할 만큼 작은 선체인지
        /// </summary>
        public static bool IsNegligible(float obstacleThickness, float ownThickness)
        {
            return obstacleThickness < ownThickness * NegligibleRatio;
        }

        /// <summary>
        /// 함재기를 뺀 선체를 모음. 부른 쪽이 Dispose 함.
        /// </summary>
        public static NativeArray<PathObstacle> Collect(EntityQuery hullQuery, ComponentLookup<PlayerSpaceshipMovementComponent> players, Allocator allocator)
        {
            using NativeArray<Entity> entities = hullQuery.ToEntityArray(Allocator.Temp);
            using NativeArray<ColliderComponent> colliders = hullQuery.ToComponentDataArray<ColliderComponent>(Allocator.Temp);
            using NativeArray<LocalTransform> transforms = hullQuery.ToComponentDataArray<LocalTransform>(Allocator.Temp);
            using NativeArray<SpaceshipSpecComponent> specs = hullQuery.ToComponentDataArray<SpaceshipSpecComponent>(Allocator.Temp);

            var obstacles = new NativeArray<PathObstacle>(entities.Length, allocator, NativeArrayOptions.UninitializedMemory);

            for (int i = 0; i < entities.Length; i++)
            {
                ColliderWorld world = ColliderOverlap.ToWorld(colliders[i], transforms[i]);

                obstacles[i] = new PathObstacle
                {
                    Entity = entities[i],
                    Center = world.Center,
                    Radius = CarrierFormation.HullRadius(world),
                    Thickness = specs[i].Thickness,
                    Player = players.HasComponent(entities[i]),
                };
            }

            return obstacles;
        }

        /// <summary>
        /// direction 으로 reach 만큼 가는 길에 걸리는 가장 가까운 선체를 비켜 가는 방향. 걸리는 선체가 없으면 direction 그대로.
        /// 이미 선체 여유 안에 있으면 그 선체에서 멀어지는 쪽으로 빠져나감. 내 두께(ownRadius x 2)에 비해 무시할 만큼 작은 선체는 뚫고 감.
        /// skipA, skipB 는 피하지 않음(e.g. 자기 자신, 노리는 대상).
        /// </summary>
        public static float2 Steer(
            float2 position,
            float2 direction,
            float reach,
            float ownRadius,
            float margin,
            NativeArray<PathObstacle> obstacles,
            Entity skipA,
            Entity skipB,
            bool skipPlayers)
        {
            float nearestEntry = float.MaxValue;
            float2 steered = direction;

            for (int i = 0; i < obstacles.Length; i++)
            {
                PathObstacle obstacle = obstacles[i];

                if (obstacle.Entity == skipA
                    || obstacle.Entity == skipB
                    || (skipPlayers && obstacle.Player)
                    || IsNegligible(obstacle.Thickness, ownRadius * 2.0F))
                {
                    continue;
                }

                float2 toObstacle = obstacle.Center - position;
                float distance = math.length(toObstacle);
                float clearance = obstacle.Radius + ownRadius + margin;

                if (distance > reach + clearance)
                {
                    continue;
                }

                float2 toCenter = toObstacle / math.max(distance, math.EPSILON);

                // 이미 겹칠 만큼 가까움. 선체 반대쪽 성분을 섞어 옆으로 빠져나감.
                if (distance <= clearance)
                {
                    if (math.dot(direction, toCenter) > 0.0F)
                    {
                        float2 across = new float2(-toCenter.y, toCenter.x);
                        across = math.dot(across, direction) >= 0.0F ? across : -across;

                        return math.normalizesafe(across - toCenter * 0.5F, direction);
                    }

                    continue;
                }

                float along = math.dot(toObstacle, direction);
                float2 side = toObstacle - direction * along;
                float miss = math.length(side);

                if (along <= 0.0F || miss >= clearance)
                {
                    continue;
                }

                float entry = along - math.sqrt(clearance * clearance - miss * miss);

                if (entry > reach || entry >= nearestEntry)
                {
                    continue;
                }

                // 길이 선체 중심에서 비껴 있는 쪽의 접선. 정면이면 왼쪽.
                float2 outward = miss > math.EPSILON ? -side / miss : new float2(-direction.y, direction.x);
                float sin = math.saturate(clearance / distance);

                nearestEntry = entry;
                steered = toCenter * math.sqrt(1.0F - sin * sin) + outward * sin;
            }

            return steered;
        }
    }
}
