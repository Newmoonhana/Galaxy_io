using GalaxyIO.DOTS.Components.Singleton;
using Unity.Entities;
using Unity.NetCode;

namespace GalaxyIO.DOTS.Components.Actor
{
    /// <summary>
    /// 우주선에 장착된 포탑 아이템. 모든 우주선이 가지며 모든 클라이언트에 복제됨.
    /// 발사, 발사음 시스템이 이 값으로 총구 사양을 찾음.
    /// </summary>
    public struct EquippedTurretComponent : IComponentData
    {
        // 없으면 None. 모든 총구가 쏘지 않음.
        [GhostField]
        public ItemId Value;
    }

    /// <summary>
    /// 포탑 장착 규칙 모음. 서버, 클라이언트가 같이 씀.
    /// </summary>
    public static class TurretEquip
    {
        /// <summary>
        /// 모든 총구가 새 포탑의 발사 간격만큼 기다리게 함. 바꿔 끼우자마자 쏘는 것 방지.
        /// 따라 쏘는 총구는 앞 총구가 쏠 때까지 기다림.
        /// </summary>
        public static void ResetCooldown(
            DynamicBuffer<TurretCooldownElement> cooldowns,
            DynamicBuffer<ItemRegistryElement> registry,
            DynamicBuffer<TurretSpecElement> specs,
            ItemId turret)
        {
            for (int i = 0; i < cooldowns.Length; i++)
            {
                TurretSpecElement spec = registry.GetTurretSpec(specs, turret, i);
                cooldowns[i] = new TurretCooldownElement
                {
                    Value = spec.FollowMuzzle >= 0 ? TurretCycle.Waiting : spec.MaxFireCooldown,
                };
            }
        }

        /// <summary>
        /// 포탑 칸이 바뀐 뒤 서버가 호출. 장착 포탑이 달라졌으면 바꾸고 쿨다운을 채움.
        /// </summary>
        public static void Refresh(
            ItemId turret,
            DynamicBuffer<ItemRegistryElement> registry,
            DynamicBuffer<TurretSpecElement> specs,
            ref EquippedTurretComponent equipped,
            DynamicBuffer<TurretCooldownElement> cooldowns)
        {
            if (equipped.Value == turret)
            {
                return;
            }

            equipped.Value = turret;
            ResetCooldown(cooldowns, registry, specs, turret);
        }
    }
}
