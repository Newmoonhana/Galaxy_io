using GalaxyIO.DOTS.Components.Actor;
using GalaxyIO.Tools;
using Newmoonhana.Runtime.Core;
using Unity.Collections;
using Unity.Entities;
using Unity.NetCode;
using UnityEngine;
using UnityEngine.Serialization;
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
        [Tooltip("게이지 배경. 열이 0 이면 숨김. 9-slice 테두리 안쪽(중앙 칸)이 채움 막대가 그려지는 홈.")]
        private Image background;

        [SerializeField]
        [Tooltip("과열 중(사격 불가) 배경. 9-slice 테두리가 평소 배경과 같아야 채움 막대 위치가 안 바뀜. 비우면 바꾸지 않음.")]
        private Sprite overheatBackground;

        [SerializeField]
        [Tooltip("열 비율만큼 늘어나는 채움 막대. 배경 홈의 왼쪽에 붙음. 열 비율에 따라 색이 바뀜.")]
        private Image fill;

        [SerializeField]
        [Tooltip("배경 9-slice 테두리 안쪽에 더 두는 여백. 0 이면 채움 막대가 홈을 꽉 채움.")]
        [Min(0.0F)]
        private float padding = 0.0F;

        [SerializeField]
        [Tooltip("열 비율(0 ~ 1)별 채움 색")]
        [FormerlySerializedAs("backgroundColor")]
        private Gradient fillColor = new Gradient();

        [SerializeField]
        [Tooltip("과열 중 채움 색. HDR 이라 1.37 을 넘는 채널이 블룸됨. Image.color 는 0 ~ 1 로 잘리므로 1 을 넘는 만큼은 채움 머티리얼(UI-HeatBar)의 _HDRColor 로 넘김.")]
        [ColorUsage(true, true)]
        private Color overheatColor = new Color(2.5F, 0.35F, 0.1F, 1.0F);

        [SerializeField]
        [Tooltip("과열 중 초당 깜빡임 수")]
        [Min(0.0F)]
        private float overheatBlinkFrequency = 4.0F;

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

        // 직전 프레임 과열 여부. 과열음과 배경 교체를 바뀔 때 한 번만 하는 데 씀.
        private bool overheated;

        // 평소 배경. 씬에 지정된 배경 스프라이트. 과열이 끝나면 되돌림.
        private Sprite normalBackground;

        // 화면에 그리는 열 비율과 그 변화 속도. 실제 열을 부드럽게 따라감.
        private float shownRatio;
        private float shownVelocity;

        // 캐시한 쿼리가 속한 클라이언트 월드. 세션 종료 시 Dispose 되므로 매 프레임 확인함.
        private World clientWorld;

        // 채움 머티리얼 인스턴스. Image.color 는 0 ~ 1 로 잘리므로 1 을 넘는 HDR 배율은 여기의 _HDRColor 로 넘김.
        private Material fillMaterial;

        private static readonly int HdrColorId = Shader.PropertyToID("_HDRColor");
        private static readonly int ShimmerStrengthId = Shader.PropertyToID("_ShimmerStrength");

        private void Start()
        {
            background.gameObject.SetActive(false);
            normalBackground = background.sprite;

            if (fill.material != null && fill.material.HasProperty(HdrColorId))
            {
                fillMaterial = new Material(fill.material);
                fill.material = fillMaterial;

                // 과열 아닌 상태로 시작. 머티리얼 에셋 값과 무관하게 shimmer 를 켬.
                if (fillMaterial.HasProperty(ShimmerStrengthId))
                {
                    fillMaterial.SetFloat(ShimmerStrengthId, 1.0F);
                }
            }

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

        private void OnDestroy()
        {
            if (fillMaterial != null)
            {
                Destroy(fillMaterial);
            }
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

            if (heat.Overheated != overheated)
            {
                if (heat.Overheated && SoundManager.Instance != null)
                {
                    SoundManager.Instance.PlaySfx(overheatSound);
                }

                // 과열 중에는 사격 불가(PlayerTurretFireSystem). 배경을 바꿔 알림.
                if (overheatBackground != null)
                {
                    background.sprite = heat.Overheated ? overheatBackground : normalBackground;
                }

                // 과열 중에는 채움 막대의 흰 shimmer 를 꺼서 빨간 깜빡임이 묻히지 않게 함.
                if (fillMaterial != null && fillMaterial.HasProperty(ShimmerStrengthId))
                {
                    fillMaterial.SetFloat(ShimmerStrengthId, heat.Overheated ? 0.0F : 1.0F);
                }
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
            RectTransform bar = fill.rectTransform;

            Color normal = fillColor.Evaluate(shownRatio);
            float blink = 0.5F + 0.5F * Mathf.Sin(Time.time * overheatBlinkFrequency * 2.0F * Mathf.PI);
            // 과열 중에는 깜빡임 바닥도 빨강. overheatColor 를 최대 채널 1 로 줄인 색 ↔ HDR 원색으로 깜빡임.
            Color shown = normal;

            if (heat.Overheated)
            {
                Color overheatBase = overheatColor / Mathf.Max(1.0F, overheatColor.maxColorComponent);
                overheatBase.a = overheatColor.a;
                shown = Color.LerpUnclamped(overheatBase, overheatColor, blink);
            }

            // 1 을 넘는 만큼은 머티리얼 배율로 넘기고 Image.color 에는 0 ~ 1 로 나눈 색을 넣음.
            float intensity = fillMaterial != null ? Mathf.Max(1.0F, shown.maxColorComponent) : 1.0F;
            fill.color = new Color(shown.r / intensity, shown.g / intensity, shown.b / intensity, shown.a);

            if (fillMaterial != null)
            {
                fillMaterial.SetColor(HdrColorId, new Color(intensity, intensity, intensity, 1.0F));
            }

            body.anchoredPosition = heat.Overheated ? Random.insideUnitCircle * shakeAmplitude : Vector2.zero;

            // 채움 막대는 배경 9-slice 테두리 안쪽(홈)에만 그림. 앵커는 배경 왼쪽 위아래, 피벗은 왼쪽 가운데.
            Vector4 inset = GetInset();
            float left = inset.x + padding;
            float bottom = inset.y + padding;
            float right = inset.z + padding;
            float top = inset.w + padding;

            bar.anchoredPosition = new Vector2(left, (bottom - top) * 0.5F);
            bar.sizeDelta = new Vector2(Mathf.Max(0.0F, body.rect.width - left - right) * shownRatio, -(top + bottom));
        }

        /// <summary>
        /// 배경 스프라이트의 9-slice 테두리를 UI 단위로 바꾼 값. (왼쪽, 아래, 오른쪽, 위)
        /// </summary>
        private Vector4 GetInset()
        {
            Sprite sprite = background.sprite;
            float pixelsPerUnit = background.pixelsPerUnit * background.pixelsPerUnitMultiplier;

            if (sprite == null || background.type != Image.Type.Sliced || pixelsPerUnit <= 0.0F)
            {
                return Vector4.zero;
            }

            return sprite.border / pixelsPerUnit;
        }
    }
}
