using GalaxyIO.DOTS.Components.Actor;
using GalaxyIO.DOTS.Components.Singleton;
using System.Collections.Generic;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;

namespace GalaxyIO.DOTS.Authoring
{
    /// <summary>
    /// 우주선의 속성과 터렛 배치 정의.
    /// </summary>
    public sealed class SpaceshipAuthoring : MonoBehaviour
    {
        [Header("Attributes")]

        // 월드 좌표 상 이동속도
        public float Speed;

        // 호버링(WASD) 최대 이동속도
        [Min(0.0F)]
        public float HoveringSpeed = 10.0F;

        // 호버링 가속도. 작을수록 옆으로 미끄러지듯 붙음.
        [Min(0.0F)]
        public float HoveringAcceleration = 40.0F;

        // 호버링 감속도. WASD를 뗐을 때 초당 줄어드는 속도.
        [Min(0.0F)]
        public float HoveringDeceleration = 60.0F;

        // 가속도
        public float Acceleration;

        // 감속도. 가속 키를 뗐을 때 초당 줄어드는 속력.
        [Min(0.0F)]
        public float Deceleration = 5.0F;

        // 회전 속도(Degree/초)
        public float Maneuver;

        // 고속 선회 억제: 최대 속력일 때 회전 속도가 깎이는 비율.
        [Range(0.0F, 1.0F)]
        public float ManeuverPenalty = 0.6F;

        // 최대 체력
        public float MaxHealth;

        // 자가 수리 속도
        public float Repair;

        // 피격 후 자가 수리가 다시 시작되기까지의 시간(초)
        [Min(0.0F)]
        public float RepairDelay = 3.0F;

        [Header("Faction")]

        // 소속 진영
        public FactionID Faction = FactionID.None;

        [Header("Turrets")]

        // 총구 위치. 포탑 사양이 이 순서대로 적용됨.
        public List<Transform> Muzzles;

        // 장착한 채로 시작하는 포탑. 아이템 레지스트리에 등록된 포탑이어야 함.
        public TurretItemAsset Turret;
    }

    public sealed class SpaceshipBaker : Baker<SpaceshipAuthoring>
    {
        public override void Bake(SpaceshipAuthoring authoring)
        {
            Entity entity = GetEntity(TransformUsageFlags.Dynamic);

            AddComponent(entity, new SpaceshipSpecComponent
            {
                MaxHealth   = authoring.MaxHealth,
                Repair      = authoring.Repair,
                RepairDelay = authoring.RepairDelay,
                Health      = authoring.MaxHealth,
            });

            AddComponent(entity, new SpaceshipMovementComponent
            {
                DeltaMovement = float2.zero
            });

            AddComponent(entity, new FactionComponent
            {
                Value = authoring.Faction,
            });

            var buffer = AddBuffer<TurretElement>(entity);

            // 서버 되감기용 충돌체 기록. 클라이언트에서는 비어 있음.
            AddBuffer<SpaceshipColliderHistory>(entity);

            AddBuffer<SpaceshipCollisionElement>(entity);

            AddComponent<CrashSoundCooldownComponent>(entity);

            DependsOn(authoring.Turret);

            ItemId turret = authoring.Turret != null ? authoring.Turret.Id : ItemId.None;

            if (authoring.Turret != null && turret == ItemId.None)
            {
                Debug.LogWarning($"Spaceship: 포탑 Id 가 None 이라 장착할 수 없음. ({authoring.Turret})", authoring);
            }

            AddComponent(entity, new EquippedTurretComponent { Value = turret });

            foreach (Transform t in authoring.Muzzles)
            {
                buffer.Add(new TurretElement
                {
                    MuzzleLocation = LocalTransform.FromPositionRotationScale(
                        t.localPosition,
                        t.localRotation,
                        t.localScale.x
                    ),
                    FireCooldown = 0.0F,
                });
            }
        }
    }
}