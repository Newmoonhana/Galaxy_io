using Unity.Entities;
using Unity.Mathematics;
using Unity.NetCode;

namespace GalaxyIO.DOTS.Components.Actor
{
    /// <summary>
    /// 포탑 열이 쌓이는 방식.
    /// </summary>
    public enum TurretHeatMode : byte
    {
        // 한 번 사격할 때마다 Heat 만큼. 총구 수와 상관없음.
        PerShot = 0,

        // 사격 시작 전환 ~ 사격 중에 초당 Heat 만큼. 채널링 무기용. e.g. Pyron, Veylance
        PerSecond = 1,
    }

    /// <summary>
    /// 플레이어 우주선의 포탑 열. 서버와 예측하는 클라이언트가 같은 규칙으로 계산함.
    /// 주인에게만 복제하며 예측 롤백 시 서버 값으로 되감음.
    /// </summary>
    [GhostComponent(OwnerSendType = SendToOwnerType.SendToOwner)]
    public struct TurretHeatComponent : IComponentData
    {
        // 최대 열. 0 이면 과열하지 않음.
        public float MaxHeat;

        [GhostField(Quantization = 0)]
        public float Heat;

        // 과열 중. 열이 0 이 될 때까지 쏠 수 없음.
        [GhostField]
        public bool Overheated;
    }

    /// <summary>
    /// 포탑 열 규칙.
    /// </summary>
    public static class TurretHeat
    {
        /// <summary>
        /// 이번 틱에 쌓인 열을 더하고 식힘. 최대 열에 닿으면 과열됨.
        /// 무사격 대기가 끝나면 최대 열에서 0 까지 coolingTime(과열 중에는 overheatCoolingTime)에 걸쳐 식음.
        /// </summary>
        public static void Update(
            ref TurretHeatComponent heat,
            float gain,
            bool standby,
            float coolingTime,
            float overheatCoolingTime,
            float deltaTime)
        {
            if (heat.MaxHeat <= 0.0F)
            {
                return;
            }

            if (heat.Overheated)
            {
                if (!standby)
                {
                    return;
                }

                heat.Heat = math.max(heat.Heat - heat.MaxHeat * deltaTime / overheatCoolingTime, 0.0F);
                heat.Overheated = heat.Heat > 0.0F;
                return;
            }

            heat.Heat += gain;

            if (heat.Heat >= heat.MaxHeat)
            {
                heat.Heat = heat.MaxHeat;
                heat.Overheated = true;
            }
            else if (standby)
            {
                heat.Heat = math.max(heat.Heat - heat.MaxHeat * deltaTime / coolingTime, 0.0F);
            }
        }
    }
}
