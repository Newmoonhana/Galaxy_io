using GalaxyIO.DOTS.Components.Singleton;
using Unity.Entities;
using Unity.NetCode;

namespace GalaxyIO.DOTS.Components.Actor
{
    /// <summary>
    /// 우주선이 마지막으로 쏜 뒤 지난 시간. 서버와 예측하는 클라이언트가 같은 규칙으로 계산함.
    /// 주인에게만 복제하며 예측 롤백 시 서버 값으로 되감음.
    /// </summary>
    [GhostComponent(OwnerSendType = SendToOwnerType.SendToOwner)]
    public struct TurretStandbyComponent : IComponentData
    {
        // 마지막 발사 후 지난 시간(초)
        [GhostField(Quantization = 0)]
        public float NoFireTime;

        // 총구가 첫 발 대기로 돌아간 상태인지. 다시 쏘면 꺼짐.
        [GhostField]
        public bool Standby;
    }

    /// <summary>
    /// 사격 대기 규칙. 플레이어, AI 발사 시스템이 같이 씀.
    /// </summary>
    public static class TurretStandby
    {
        /// <summary>
        /// 쏘지 않는 동안 총구 하나라도 발사 준비가 끝났으면 true. 이때는 모든 총구의 쿨다운을 멈춤.
        /// 다른 총구가 먼저 0까지 내려가 총구 사이 발사 간격이 무너지는 것 방지.
        /// </summary>
        public static bool ShouldHold(
            bool triggered,
            DynamicBuffer<TurretCooldownElement> cooldowns,
            DynamicBuffer<ItemRegistryElement> registry,
            DynamicBuffer<TurretSpecElement> specs,
            ItemId turret)
        {
            if (triggered)
            {
                return false;
            }

            for (int i = 0; i < cooldowns.Length; i++)
            {
                if (cooldowns[i].Value <= 0.0F
                    && registry.GetTurretSpec(specs, turret, i).ProjectilePrefab != Entity.Null)
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// 이번 틱 발사 여부를 반영함. 오래 쏘지 않았고 쿨다운이 멈춘 상태면 모든 총구를 첫 발 대기로 되돌림.
        /// 쿨다운이 아직 도는 중에는 되돌리지 않음. 발사 간격보다 일찍 쏘는 것 방지.
        /// </summary>
        public static void Update(
            ref TurretStandbyComponent standby,
            bool fired,
            bool held,
            float deltaTime,
            float standbyTime,
            DynamicBuffer<TurretCooldownElement> cooldowns,
            DynamicBuffer<ItemRegistryElement> registry,
            DynamicBuffer<TurretSpecElement> specs,
            ItemId turret)
        {
            if (fired)
            {
                standby.NoFireTime = 0.0F;
                standby.Standby = false;
                return;
            }

            // 대기 상태에서는 더 세지 않음. 값이 그대로면 스냅샷에 실리지 않음.
            if (standby.Standby)
            {
                return;
            }

            standby.NoFireTime += deltaTime;

            if (!held || standby.NoFireTime < standbyTime)
            {
                return;
            }

            standby.Standby = true;

            for (int i = 0; i < cooldowns.Length; i++)
            {
                cooldowns[i] = new TurretCooldownElement
                {
                    Value = registry.GetTurretSpec(specs, turret, i).FirstShotCooldown,
                };
            }
        }
    }
}
