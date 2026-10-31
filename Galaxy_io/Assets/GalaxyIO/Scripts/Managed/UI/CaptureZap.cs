using GalaxyIO.DOTS.Components.Actor;
using GalaxyIO.DOTS.Components.Singleton;
using UnityEngine;

namespace GalaxyIO.Scripts.Managed.UI
{
    /// <summary>
    /// 점령전 경보와 위성 글자 번쩍임을 맞추는 곳. <see cref="ModeAlertView"/>가 경보를 울리는 순간 <see cref="Trigger"/>를
    /// 부르고, <see cref="GameModeHudView"/>는 그때부터 <see cref="Duration"/> 동안 경보 대상 위성 글자를 흔들고 번쩍임.
    /// 경보가 울리지 않으면 번쩍이지도 않음.
    /// </summary>
    public static class CaptureZap
    {
        // 경보 주기(초). OccupyAlarm.ogg(약 2.8초) 한 번이 이 안에 끝남.
        public const float Period = 3.0F;

        // 한 번 번쩍이고 흔드는 시간(초)
        public const float Duration = 1.35F;

        // 끝에서 원래 색으로 돌아오는 시간(초). 그 전까지는 최대 세기를 유지함.
        private const float FadeOut = 0.25F;

        // 마지막으로 경보를 울린 시각(unscaledTime)
        private static float triggeredAt = float.NegativeInfinity;

        /// <summary>
        /// 지금 번쩍임 세기(0 ~ 1). 경보가 울린 뒤 <see cref="Duration"/> 동안 1 을 유지하다 끝에서 줄어듦.
        /// </summary>
        public static float Strength
        {
            get
            {
                float elapsed = Time.unscaledTime - triggeredAt;
                return elapsed < 0.0F || elapsed >= Duration ? 0.0F : Mathf.Clamp01((Duration - elapsed) / FadeOut);
            }
        }

        /// <summary>
        /// 경보를 울린 순간 부름.
        /// </summary>
        public static void Trigger()
        {
            triggeredAt = Time.unscaledTime;
        }

        /// <summary>
        /// 멈춤. 판이 끝나거나 경보가 멈추면 부름.
        /// </summary>
        public static void Stop()
        {
            triggeredAt = float.NegativeInfinity;
        }

        /// <summary>
        /// 이 팀에게 경보를 울릴 위성인지. 우리 위성에 상대 팀 점령도가 쌓여 있음.
        /// </summary>
        public static bool IsAlarming(in SatelliteComponent satellite, Team team)
        {
            return team != Team.None
                   && satellite.Owner == team
                   && satellite.Capturing == Teams.Other(team)
                   && satellite.Progress > 0.0F;
        }
    }
}
