using GalaxyIO.DOTS.Components.Singleton;
using GalaxyIO.DOTS.Systems;
using Unity.Entities;
using Unity.Mathematics;
using Unity.NetCode;
using Unity.Transforms;

namespace GalaxyIO.DOTS.Components.Actor
{
    /// <summary>
    /// 자동 포탑 칸 하나의 장착 포탑과 조준 상태. <see cref="AutoTurretMountElement"/>와 같은 순서.
    /// 서버의 <see cref="AutoTurretFireSystem"/>이 갱신하고 모든 클라이언트에 복제함.
    /// </summary>
    public struct AutoTurretElement : IBufferElementData
    {
        // 장착한 포탑. None 이면 빈 칸.
        [GhostField]
        public ItemId Turret;

        // 처음 방향 기준 회전각(라디안). 반시계가 +. 시야각 절반을 넘지 않음. 시야각 제한이 없으면 -π ~ π.
        [GhostField(Quantization = 1000)]
        public float Angle;

        // 사격 중인지. 클라이언트의 광선, 직접 발사 투사체, 반복음이 씀.
        [GhostField]
        public bool Firing;
    }

    /// <summary>
    /// 자동 포탑 칸 하나의 위치와 조준 진행 상태. 복제하지 않음.
    /// </summary>
    public struct AutoTurretMountElement : IBufferElementData
    {
        // 돌아가는 포탑 Entity. 클라이언트가 Angle 만큼 돌려 그림.
        public Entity Turret;

        // 포탑 그림(SpriteRenderer) Entity. 클라이언트가 빈 칸이면 감춤.
        public Entity Sprite;

        // 포탑의 처음 로컬 회전
        public quaternion Rest;

        // 우주선 기준 포탑 회전 중심
        public float2 Pivot;

        // 처음 방향일 때 회전 중심에서 총구까지. 이 방향이 시야각의 가운데.
        public float2 Barrel;

        // 서버는 발사 쿨다운, 클라이언트는 직접 발사 투사체의 쿨다운.
        public float Cooldown;

        // 서버만. 사격/휴식 주기의 남은 시간과 휴식 중인지.
        public float CycleRemaining;
        public bool Resting;

        // 서버만. 대상이 없을 때 훑는 방향(+1, -1)과 끝에서 기다릴 남은 시간.
        public float SweepDirection;
        public float SweepWait;

        // 서버만. 조준 중인 대상. 없으면 Entity.Null.
        public Entity Target;

        // 클라이언트만. 복제된 Angle 을 부드럽게 따라가는 그리기 각도.
        public float VisualAngle;
    }

    /// <summary>
    /// 자동 포탑 규칙. 서버 발사, 클라이언트 연출이 같이 씀.
    /// </summary>
    public static class AutoTurret
    {
        /// <summary>
        /// 각도를 -π ~ π 로 맞춤.
        /// </summary>
        public static float Wrap(float angle)
        {
            return angle - math.round(angle / (2.0F * math.PI)) * (2.0F * math.PI);
        }

        /// <summary>
        /// angle 만큼 돈 총구의 우주선 기준 위치와 회전. 회전은 투사체가 나가는 방향이 +Y.
        /// </summary>
        public static LocalTransform MuzzleLocation(in AutoTurretMountElement mount, float angle)
        {
            float2 barrel = math.mul(float2x2.Rotate(angle), mount.Barrel);

            return LocalTransform.FromPositionRotation(
                new float3(mount.Pivot + barrel, 0.0F),
                quaternion.RotateZ(math.atan2(-barrel.x, barrel.y))
            );
        }
    }
}
