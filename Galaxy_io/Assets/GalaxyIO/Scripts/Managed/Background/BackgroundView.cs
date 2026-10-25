using UnityEngine;

namespace GalaxyIO.Scripts.Managed.Background
{
    /// <summary>
    /// 배경 겹을 카메라에 붙여 화면을 덮는 클라이언트 전용 컴포넌트.
    /// 품질 단계에 따라 별 겹 수를 줄임.
    /// </summary>
    [DefaultExecutionOrder(100)]
    public sealed class BackgroundView : MonoBehaviour
    {
        // Background-Stars 그래프의 Star Octaves 키워드. 인덱스 + 1 = 겹 수.
        private static readonly string[] StarOctaveKeywords =
        {
            "_STAR_OCTAVES_ONE",
            "_STAR_OCTAVES_TWO",
            "_STAR_OCTAVES_THREE",
        };

        [SerializeField]
        [Tooltip("배경이 따라다닐 카메라. 비우면 Camera.main 을 씀.")]
        private UnityEngine.Camera targetCamera;

        [SerializeField]
        [Tooltip("화면을 덮는 겹들. 전부 Background 정렬 레이어에 둠.")]
        private SpriteRenderer[] layers;

        [SerializeField]
        [Tooltip("화면 크기 대비 쿼드 크기. 카메라 흔들림과 시야 보간 중에도 가장자리가 안 보이게 여유를 둠.")]
        [Range(1.0F, 2.0F)]
        private float coverMargin = 1.2F;

        [SerializeField]
        [Tooltip("품질 단계별 별 겹 수. 인덱스 = QualitySettings 단계. 모자라면 마지막 값을 씀.")]
        private int[] starOctavesByQuality = { 1, 2, 3 };

        // 마지막으로 맞춘 화면 크기. 같으면 Transform 을 안 건드림.
        private Vector2 appliedSize;

        // 마지막으로 켠 겹 수. 0 이면 아직 안 켬.
        private int appliedOctaves;

        private void Awake()
        {
            if (targetCamera == null)
            {
                targetCamera = UnityEngine.Camera.main;
            }
        }

        private void OnEnable()
        {
            appliedSize = Vector2.zero;
            QualitySettings.activeQualityLevelChanged += OnQualityChanged;
            ApplyStarOctaves();
        }

        private void OnDisable()
        {
            QualitySettings.activeQualityLevelChanged -= OnQualityChanged;
        }

        private void OnQualityChanged(int previous, int current)
        {
            ApplyStarOctaves();
        }

        private void LateUpdate()
        {
            if (targetCamera == null)
            {
                return;
            }

            // 카메라가 흔들리고 시야가 보간되는 중에도 쿼드가 화면 중앙에 붙어 있어야 함.
            Vector3 cameraPosition = targetCamera.transform.position;
            transform.position = new Vector3(cameraPosition.x, cameraPosition.y, transform.position.z);

            Vector2 size = new Vector2(
                targetCamera.orthographicSize * targetCamera.aspect,
                targetCamera.orthographicSize) * (2.0F * coverMargin);

            if ((size - appliedSize).sqrMagnitude < 1e-6F)
            {
                return;
            }

            appliedSize = size;

            for (int i = 0; i < layers.Length; i++)
            {
                SpriteRenderer layer = layers[i];

                if (layer == null || layer.sprite == null)
                {
                    continue;
                }

                // 스프라이트의 PPU 와 무관하게 화면을 덮게 함.
                Vector2 bounds = layer.sprite.bounds.size;

                layer.transform.localScale = new Vector3(
                    size.x / Mathf.Max(bounds.x, 1e-4F),
                    size.y / Mathf.Max(bounds.y, 1e-4F),
                    1.0F);
            }
        }

        /// <summary>
        /// 품질 단계에 맞는 별 겹 수 키워드를 켬. 전역 키워드라 머티리얼 에셋이 더러워지지 않음.
        /// </summary>
        private void ApplyStarOctaves()
        {
            if (starOctavesByQuality == null || starOctavesByQuality.Length == 0)
            {
                return;
            }

            int level = Mathf.Clamp(QualitySettings.GetQualityLevel(), 0, starOctavesByQuality.Length - 1);
            int octaves = Mathf.Clamp(starOctavesByQuality[level], 1, StarOctaveKeywords.Length);

            if (octaves == appliedOctaves)
            {
                return;
            }

            appliedOctaves = octaves;

            for (int i = 0; i < StarOctaveKeywords.Length; i++)
            {
                if (i == octaves - 1)
                {
                    Shader.EnableKeyword(StarOctaveKeywords[i]);
                }
                else
                {
                    Shader.DisableKeyword(StarOctaveKeywords[i]);
                }
            }
        }
    }
}
