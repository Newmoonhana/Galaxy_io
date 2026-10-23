using GalaxyIO.DOTS.Components.Actor;
using GalaxyIO.Tools;
using Newmoonhana.Runtime.Core;
using Unity.Collections;
using Unity.Entities;
using Unity.NetCode;
using UnityEngine;
using UnityEngine.UI;

namespace GalaxyIO.Scripts.Managed.UI
{
    /// <summary>
    /// 로컬 우주선 체력이 낮을 때 화면 가장자리를 붉게 맥동시키고 경보음을 내는 클라이언트 전용 뷰.
    /// 경보음은 포탑 과열 중에도 냄. 두 상태가 모두 끝날 때까지 한 번만 반복 재생함.
    /// </summary>
    public sealed class LowHealthView : MonoBehaviour
    {
        [SerializeField]
        [Tooltip("화면 전체를 덮는 비네트. 가운데가 투명한 텍스처.")]
        private RawImage vignette;

        [SerializeField]
        [Tooltip("이 체력 비율 미만이면 경고")]
        [Range(0.0F, 1.0F)]
        private float threshold = 0.2F;

        [Header("Vignette")]

        [SerializeField]
        [Tooltip("비네트 색. 알파가 최대 진하기.")]
        private Color color = new Color(1.0F, 0.0F, 0.0F, 0.6F);

        [SerializeField]
        [Tooltip("나타나는 시간(초)")]
        [Min(0.01F)]
        private float fadeIn = 0.3F;

        [SerializeField]
        [Tooltip("체력이 회복된 뒤 사라지는 시간(초)")]
        [Min(0.01F)]
        private float fadeOut = 1.5F;

        [SerializeField]
        [Tooltip("맥동할 때 커지는 비율. 커질수록 붉은 테두리가 화면 밖으로 밀려나 얇아짐.")]
        [Min(0.0F)]
        private float pulseScale = 0.12F;

        [SerializeField]
        [Tooltip("초당 맥동 수")]
        [Min(0.0F)]
        private float pulseFrequency = 1.2F;

        [Header("Alarm")]

        [SerializeField]
        [Tooltip("체력이 낮거나 과열 중일 때 반복 재생")]
        private string alarmSound = "Loop_Alarm";

        [SerializeField]
        [Tooltip("체력이 회복되어 경보음이 멈출 때 0 까지 줄어드는 시간(초)")]
        [Min(0.01F)]
        private float healthAlarmFadeOut = 1.5F;

        [SerializeField]
        [Tooltip("과열이 끝나 경보음이 멈출 때 0 까지 줄어드는 시간(초)")]
        [Min(0.01F)]
        private float heatAlarmFadeOut = 0.3F;

        private EntityQuery shipQuery;
        private EntityQuery heatQuery;

        // 비네트 진하기(0 ~ 1)
        private float weight;

        // 경보음 재생 여부와 직전 프레임의 체력 경고 여부. 멈출 때 줄어드는 시간을 고르는 데 씀.
        private bool alarm;
        private bool lowHealth;

        // 캐시한 쿼리가 속한 클라이언트 월드. 세션 종료 시 Dispose 되므로 매 프레임 확인함.
        private World clientWorld;

        private void Start()
        {
            vignette.enabled = false;
            clientWorld = ClientServerBootstrap.ClientWorld;

            if (clientWorld == null)
            {
                enabled = false;
                return;
            }

            shipQuery = clientWorld.EntityManager.CreateEntityQuery(
                ComponentType.ReadOnly<GhostOwnerIsLocal>(),
                ComponentType.ReadOnly<PlayerSpaceshipMovementComponent>(),
                ComponentType.ReadOnly<SpaceshipSpecComponent>()
            );
            heatQuery = clientWorld.EntityManager.CreateEntityQuery(
                ComponentType.ReadOnly<GhostOwnerIsLocal>(),
                ComponentType.ReadOnly<TurretHeatComponent>()
            );
        }

        private void LateUpdate()
        {
            // 세션을 나가면 클라이언트 월드가 Dispose 됨. 캐시한 쿼리를 더 쓰면 안 됨.
            bool alive = ClientWorldScope.IsAlive(clientWorld);

            // 우주선이 없으면(파괴, 리스폰 대기) 경고하지 않음.
            bool low = false;
            bool overheated = false;

            if (alive && !shipQuery.IsEmpty)
            {
                using var specs = shipQuery.ToComponentDataArray<SpaceshipSpecComponent>(Allocator.Temp);
                low = specs[0].MaxHealth > 0.0F && specs[0].Health < specs[0].MaxHealth * threshold;
            }

            if (alive && !heatQuery.IsEmpty)
            {
                using var heats = heatQuery.ToComponentDataArray<TurretHeatComponent>(Allocator.Temp);
                overheated = heats[0].Overheated;
            }

            // 두 상태 중 하나라도 있으면 계속 재생함. 마지막에 끝난 상태의 시간으로 줄여 멈춤.
            bool next = low || overheated;

            if (next != alarm && SoundManager.Instance != null)
            {
                if (next)
                {
                    SoundManager.Instance.PlaySfx(alarmSound);
                }
                else
                {
                    SoundManager.Instance.FadeOutSfx(alarmSound, lowHealth ? healthAlarmFadeOut : heatAlarmFadeOut);
                }
            }

            alarm = next;
            lowHealth = low;

            if (!alive)
            {
                vignette.enabled = false;
                enabled = false;
                return;
            }

            weight = Mathf.MoveTowards(weight, low ? 1.0F : 0.0F, Time.deltaTime / (low ? fadeIn : fadeOut));
            vignette.enabled = weight > 0.0F;

            if (!vignette.enabled)
            {
                return;
            }

            // 1 보다 작아지면 화면 가장자리가 드러나므로 1 ~ 1 + pulseScale 사이에서 맥동함.
            float pulse = 0.5F + 0.5F * Mathf.Sin(Time.time * pulseFrequency * 2.0F * Mathf.PI);

            vignette.rectTransform.localScale = Vector3.one * (1.0F + pulseScale * pulse);
            vignette.color = new Color(color.r, color.g, color.b, color.a * weight);
        }
    }
}
