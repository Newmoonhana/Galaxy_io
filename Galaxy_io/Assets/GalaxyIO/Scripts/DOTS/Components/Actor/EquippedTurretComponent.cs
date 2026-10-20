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
        /// 인벤토리에 조종 포탑 칸이 있는지. 조종 포탑 총구가 있고 고정 포탑(<see cref="FixedTurretTag"/>)이 아니어야 함.
        /// </summary>
        public static bool HasMainTurretSlot(EntityManager entityManager, Entity ship)
        {
            return entityManager.GetBuffer<TurretElement>(ship, true).Length > 0
                   && !entityManager.HasComponent<FixedTurretTag>(ship);
        }

        /// <summary>
        /// 인벤토리 포탑 칸 번호의 자동 포탑 순번. 조종 포탑 칸이 있는 기종은 0 번 칸이 조종 포탑이라 -1.
        /// </summary>
        public static int AutoTurretIndex(int slot, bool hasMainTurret)
        {
            return hasMainTurret ? slot - 1 : slot;
        }

        /// <summary>
        /// 첫 총구가 새 포탑의 발사 간격만큼 기다리게 함. 바꿔 끼우자마자 쏘는 것 방지.
        /// 뒤 총구는 첫 총구가 쏠 때까지 기다림.
        /// </summary>
        public static void ResetCooldown(DynamicBuffer<TurretCooldownElement> cooldowns, in TurretSpecData spec)
        {
            for (int i = 0; i < cooldowns.Length; i++)
            {
                cooldowns[i] = new TurretCooldownElement { Value = i == 0 ? spec.MaxFireCooldown : TurretCycle.Waiting };
            }
        }

        /// <summary>
        /// 포탑 칸이 바뀐 뒤 서버가 호출. 장착 포탑이 달라졌으면 바꾸고 쿨다운을 채움.
        /// </summary>
        public static void Refresh(
            ItemId turret,
            DynamicBuffer<ItemRegistryElement> registry,
            ref EquippedTurretComponent equipped,
            DynamicBuffer<TurretCooldownElement> cooldowns)
        {
            if (equipped.Value == turret)
            {
                return;
            }

            equipped.Value = turret;
            ResetCooldown(cooldowns, registry.Get(turret).Turret);
        }    }
}
