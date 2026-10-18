using TMPro;
using UnityEngine;
using UnityEngine.Serialization;
using UnityEngine.UI;

namespace GalaxyIO.Scripts.Managed.UI
{
    /// <summary>
    /// 핑 프리팹 하나의 뷰. 화면 안이면 원, 화면 밖이면 화살표를 그리고, 그 옆에 글자를 붙임.
    /// 피벗은 원의 중심 또는 화살표 끝.
    /// </summary>
    public sealed class PingMarkerView : MonoBehaviour
    {
        private static readonly int PaddingId = Shader.PropertyToID("_Padding");

        [SerializeField]
        [Tooltip("UI-PingArrow 머티리얼을 쓰는 이미지")]
        [FormerlySerializedAs("arrow")]
        private Image marker;

        [SerializeField]
        [Tooltip("화면 밖 핑의 화살표. UI-PingArrow, Circle 꺼짐.")]
        private Material arrowMaterial;

        [SerializeField]
        [Tooltip("화면 안 핑의 원. UI-PingArrow, Circle 켜짐.")]
        private Material circleMaterial;

        [SerializeField]
        [Tooltip("삼각형 한 변의 크기(캔버스 단위). 빛번짐 자리는 따로 더해짐.")]
        [Min(1.0F)]
        private float arrowSize = 16.0F;

        [SerializeField]
        [Tooltip("원의 지름(캔버스 단위). 빛번짐 자리는 따로 더해짐.")]
        [Min(1.0F)]
        private float circleSize = 10.0F;

        [SerializeField]
        [Tooltip("리치 텍스트를 꺼 둬야 함. 가운데 정렬. 크기는 글자에 맞춰 늘어남.")]
        private TMP_Text label;

        [SerializeField]
        [Tooltip("원 / 화살표와 글자 사이 간격")]
        [Min(0.0F)]
        private float labelGap = 4.0F;

        // 지금 그리는 모양. 바뀔 때만 머티리얼과 크기를 다시 씀.
        private bool? circle;

        /// <summary>
        /// 표시 기준점의 화면 좌표와 화살표 방향. 화면 가장자리 여백(margin, 픽셀) 안이면 핑 자리(원의 중심)이고 방향은 0,
        /// 밖이면 여백 경계에 붙인 화살표 끝과 핑 쪽 방향.
        /// </summary>
        public static Vector2 Anchor(UnityEngine.Camera mainCamera, Vector2 worldPosition, float margin, out Vector2 direction)
        {
            Vector2 screen = mainCamera.WorldToScreenPoint(worldPosition);
            Vector2 center = new Vector2(Screen.width, Screen.height) * 0.5F;
            Vector2 half = Vector2.Max(center - Vector2.one * margin, Vector2.one);
            Vector2 offset = screen - center;

            if (Mathf.Abs(offset.x) <= half.x && Mathf.Abs(offset.y) <= half.y)
            {
                direction = Vector2.zero;
                return screen;
            }

            direction = offset.normalized;
            return center + offset * Mathf.Min(half.x / Mathf.Abs(offset.x), half.y / Mathf.Abs(offset.y));
        }

        /// <summary>
        /// direction 이 0 이면 원을, 아니면 그쪽을 향한 화살표를 그림.
        /// 글자는 side.x 쪽 옆에 붙이고, side.y 가 양수면 위 끝을, 음수면 아래 끝을 그림과 맞춤.
        /// </summary>
        public void Place(Vector2 direction, Vector2 side)
        {
            bool inside = direction == Vector2.zero;
            float size = inside ? circleSize : arrowSize;

            if (circle != inside)
            {
                circle = inside;
                marker.material = inside ? circleMaterial : arrowMaterial;

                // 빛번짐 자리만큼 이미지를 키우고, 원의 중심 또는 삼각형 꼭짓점이 피벗에 오게 맞춤.
                float padding = marker.material.GetFloat(PaddingId);

                marker.rectTransform.sizeDelta = Vector2.one * (size / (1.0F - 2.0F * padding));
                marker.rectTransform.pivot = inside ? new Vector2(0.5F, 0.5F) : new Vector2(0.5F, 1.0F - padding);
                marker.rectTransform.localRotation = Quaternion.identity;
            }

            if (!inside)
            {
                marker.rectTransform.localRotation = Quaternion.Euler(0.0F, 0.0F, Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg - 90.0F);
            }

            // 화살표는 끝에서 몸통 쪽으로 반만큼 들어간 곳이 그림의 중심.
            Vector2 center = -direction * (size * 0.5F);

            label.rectTransform.pivot = new Vector2(side.x > 0.0F ? 0.0F : 1.0F, side.y > 0.0F ? 1.0F : 0.0F);
            label.rectTransform.localPosition = center + new Vector2(side.x * (size * 0.5F + labelGap), side.y * size * 0.5F);
        }

        public void Set(string text, Color color)
        {
            label.text = text;
            label.color = color;
            label.rectTransform.sizeDelta = label.GetPreferredValues(text);
            marker.color = color;
        }
    }
}
