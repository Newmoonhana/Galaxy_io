using GalaxyIO.DOTS.Components.Singleton;
using Unity.Entities;
using Unity.Mathematics;
using Unity.NetCode;

namespace GalaxyIO.DOTS.Components.Actor
{
    /// <summary>
    /// 우주선의 사격 전환 단계.
    /// </summary>
    public enum TurretSwitchState : byte
    {
        // 쏘지 않음
        Idle = 0,

        // 사격 시작 전환 중. 쏠 수 없음. 충전 포탑은 충전 중.
        SpinningUp = 1,

        // 사격 중. 충전 포탑은 쏘는 한 틱.
        Firing = 2,

        // 사격 중지 전환 중. 쏠 수 없음. 충전 포탑은 다 차기 전에 뗀 한 틱.
        SpinningDown = 3,
    }

    /// <summary>
    /// 우주선 단위 사격/중지 전환 상태. 전환 시간은 장착 포탑의 SwitchTime.
    /// 모든 클라이언트에 복제함. 반복 발사음이 이 값을 씀. 주인은 예측 롤백으로 서버 값에 맞춤.
    /// </summary>
    public struct TurretSwitchComponent : IComponentData
    {
        [GhostField]
        public TurretSwitchState State;

        // 남은 전환 시간(초)
        [GhostField(Quantization = 0)]
        public float Remaining;
    }

    /// <summary>
    /// 사격 전환 규칙. 플레이어, AI 의 발사/조종 시스템이 같이 씀.
    /// </summary>
    public static class TurretSwitch
    {
        /// <summary>
        /// 이번 틱 사격 입력을 반영하고 지금 쏠 수 있는지 반환함.
        /// 전환 시간이 0 이면 입력이 곧 사격 여부.
        /// </summary>
        public static bool Update(ref TurretSwitchComponent turretSwitch, bool trigger, float switchTime, float deltaTime)
        {
            switch (turretSwitch.State)
            {
                case TurretSwitchState.Idle:
                    if (trigger)
                    {
                        turretSwitch.State = switchTime > 0.0F ? TurretSwitchState.SpinningUp : TurretSwitchState.Firing;
                        turretSwitch.Remaining = switchTime;
                    }
                    break;

                case TurretSwitchState.SpinningUp:
                    turretSwitch.Remaining -= deltaTime;

                    // 시작 전환 중에 떼도 중지 전환을 거침. 연타로 전환 시간을 건너뛰지 못함.
                    if (!trigger)
                    {
                        turretSwitch.State = TurretSwitchState.SpinningDown;
                        turretSwitch.Remaining = switchTime;
                    }
                    else if (turretSwitch.Remaining <= 0.0F)
                    {
                        turretSwitch.State = TurretSwitchState.Firing;
                    }
                    break;

                case TurretSwitchState.Firing:
                    if (!trigger)
                    {
                        turretSwitch.State = switchTime > 0.0F ? TurretSwitchState.SpinningDown : TurretSwitchState.Idle;
                        turretSwitch.Remaining = switchTime;
                    }
                    break;

                case TurretSwitchState.SpinningDown:
                    turretSwitch.Remaining -= deltaTime;

                    if (turretSwitch.Remaining <= 0.0F)
                    {
                        turretSwitch.State = TurretSwitchState.Idle;
                    }
                    break;
            }

            return turretSwitch.State == TurretSwitchState.Firing;
        }

        /// <summary>
        /// 충전 포탑(<see cref="ItemRegistryElement.ReleaseFire"/>)의 사격 입력을 반영하고 이번 틱에 쏘는지 반환함.
        /// 누르는 동안 충전(SpinningUp)하고 Remaining 은 0 에서 멈춤. 다 찬 뒤 떼면 Firing, 다 차기 전에 떼면 SpinningDown.
        /// Firing, SpinningDown 은 한 틱만 머묾. SpinningDown 의 Remaining 은 모자란 충전 시간(0 초과).
        /// </summary>
        /// <param name="ready">쿨다운이 끝남. 꺼져 있으면 충전을 시작하지 않음.</param>
        public static bool UpdateCharge(ref TurretSwitchComponent turretSwitch, bool trigger, bool ready, float chargeTime, float deltaTime)
        {
            switch (turretSwitch.State)
            {
                case TurretSwitchState.Idle:
                    if (trigger && ready)
                    {
                        turretSwitch.State = TurretSwitchState.SpinningUp;
                        turretSwitch.Remaining = chargeTime;
                    }
                    break;

                case TurretSwitchState.SpinningUp:
                    // 틱을 빼며 쌓인 오차로 반 틱도 안 남으면 다 찬 것으로 봄.
                    turretSwitch.Remaining -= deltaTime;
                    if (turretSwitch.Remaining < deltaTime * 0.5F) turretSwitch.Remaining = 0.0F;

                    if (!trigger)
                    {
                        turretSwitch.State = turretSwitch.Remaining <= 0.0F
                            ? TurretSwitchState.Firing
                            : TurretSwitchState.SpinningDown;
                    }
                    break;

                default:
                    turretSwitch.State = TurretSwitchState.Idle;
                    break;
            }

            return turretSwitch.State == TurretSwitchState.Firing;
        }

        /// <summary>
        /// 우주선 회전속도에 곱할 배율. 사격 중이면 장착 포탑의 ManeuverPenalty 만큼 줄어듦.
        /// </summary>
        public static float ManeuverScale(in TurretSwitchComponent turretSwitch, in ItemRegistryElement turret)
        {
            return turretSwitch.State == TurretSwitchState.Firing ? 1.0F - math.saturate(turret.ManeuverPenalty) : 1.0F;
        }
    }
}
