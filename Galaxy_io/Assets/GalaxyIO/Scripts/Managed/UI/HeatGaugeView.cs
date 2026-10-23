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
    /// 로컬 플레이어 우주선의 포탑 열을 막대로 띄우는 클라이언트 전용 뷰. 과열음도 재생함.
    /// 과열 중 경보음은 <see cref="LowHealthView"/>가 냄.
    /// </summary>
    public sealed class HeatGaugeView : MonoBehaviour
    {
        [SerializeField]
        [Tooltip("열 비율에 따라 색이 바뀌는 배경. 열이 0 이면 숨김.")]
        private Image background;

        [SerializeField]
        [Tooltip("열 비율만큼 늘어나는 채움 막대. 배경의 왼쪽에 붙음.")]
        private RectTransform fill;

        [SerializeField]
        [Tooltip("배경과 채움 막대 사이 여백")]
        [Min(0.0F)]
        private float padding = 3.0F;

        [SerializeField]
        [Tooltip("열 비율(0 ~ 1)별 배경색. 과열 중에는 1 의 색.")]
        private Gradient backgroundColor = new Gradient();

        [SerializeField]
        [Tooltip("표시 비율이 실제 열을 따라가는 데 걸리는 시간(초)")]
        [Min(0.0F)]
        private float smoothTime = 0.1F;

        [SerializeField]
        [Tooltip("과열 중 흔들림 폭")]
        [Min(0.0F)]
        private float shakeAmplitude = 3.0F;

        [SerializeField]
        [Tooltip("과열될 때 한 번 재생")]
        private string overheatSound = "Overheat";

        private EntityQuery heatQuery;

        // 직전 프레임 과열 여부. 과열음을 한 번만 내는 데 씀.
        private bool overheated;

        // 화면에 그리는 열 비율과 그 변화 속도. 실제 열을 부드럽게 따라감.
        private float shownRatio;
        private float shownVelocity;

        // 캐시한 쿼리가 속한 클라이언트 월드. 세션 종료 시 Dispose 되므로 매 프레임 확인함.
        private World clientWorld;

        private void Start()
        {
            background.gameObject.SetActive(false);
            clientWorld = ClientServerBootstrap.ClientWorld;

            if (clientWorld == null)
            {
                enabled = false;
                return;
            }

            heatQuery = clientWorld.EntityManager.CreateEntityQuery(
                ComponentType.ReadOnly<GhostOwnerIsLocal>(),
                ComponentType.ReadOnly<TurretHeatComponent>()
            );
        }

        private void LateUpdate()
        {
            // 세션을 나가면 클라이언트 월드가 Dispose 됨. 캐시한 쿼리를 더 쓰면 안 됨.
            bool alive = ClientWorldScope.IsAlive(clientWorld);

            // 우주선이 없으면(파괴, 리스폰 대기) 열 0 으로 다룸.
            TurretHeatComponent heat = default;

            if (alive && !heatQuery.IsEmpty)
            {
                using var heats = heatQuery.ToComponentDataArray<TurretHeatComponent>(Allocator.Temp);
                heat = heats[0];
            }

            if (heat.Overheated && !overheated && SoundManager.Instance != null)
            {
                SoundManager.Instance.PlaySfx(overheatSound);
            }

            overheated = heat.Overheated;

            if (!alive)
            {
                background.gameObject.SetActive(false);
                enabled = false;
                return;
            }

            float ratio = heat.MaxHeat > 0.0F ? Mathf.Clamp01(heat.Heat / heat.MaxHeat) : 0.0F;
            shownRatio = Mathf.SmoothDamp(shownRatio, ratio, ref shownVelocity, smoothTime);

            // 열이 0 이 된 뒤 막대가 다 줄어들면 숨김.
            if (ratio <= 0.0F && shownRatio < 0.001F)
            {
                shownRatio = 0.0F;
                shownVelocity = 0.0F;
            }

            bool visible = shownRatio > 0.0F;
            background.gameObject.SetActive(visible);

            if (!visible)
            {
                return;
            }

            RectTransform body = background.rectTransform;

            background.color = backgroundColor.Evaluate(heat.Overheated ? 1.0F : shownRatio);
            body.anchoredPosition = heat.Overheated ? Random.insideUnitCircle * shakeAmplitude : Vector2.zero;

            fill.anchoredPosition = new Vector2(padding, 0.0F);
            fill.sizeDelta = new Vector2((body.rect.width - padding * 2.0F) * shownRatio, -padding * 2.0F);
        }
    }
}
