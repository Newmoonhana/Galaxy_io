using UnityEngine;

namespace GalaxyIO.Scripts.Managed.UI
{
    /// <summary>
    /// 체력바 프리팹의 채움 막대를 비율대로 줄이는 뷰.
    /// </summary>
    public sealed class HealthBarView : MonoBehaviour
    {
        [SerializeField]
        [Tooltip("체력 비율만큼 X로 줄어드는 채움 막대")]
        private Transform fill;

        // 프리팹에 저장된 원래 크기. 비율을 곱할 기준임.
        private Vector3 fullScale;

        private void Awake()
        {
            fullScale = fill.localScale;
        }

        /// <summary>
        /// 체력 비율(0 ~ 1)을 반영함.
        /// </summary>
        public void SetRatio(float ratio)
        {
            fill.localScale = new Vector3(fullScale.x * ratio, fullScale.y, fullScale.z);

            // 가운데 피벗이라 줄어든 만큼 왼쪽으로 밀어야 왼쪽 끝이 고정됨.
            fill.localPosition = new Vector3(fullScale.x * (ratio - 1.0F) * 0.5F, 0.0F, 0.0F);
        }
    }
}