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
        // BackgroundCommon.hlsl 이 선언한 전역들. 머티리얼 프로퍼티가 아님.
        private static readonly int StarShapesId = Shader.PropertyToID("_StarShapes");
        private static readonly int CameraOffsetId = Shader.PropertyToID("_BgCameraOffset");
        private static readonly int FadeOutId = Shader.PropertyToID("_BgFadeOut");

        // 씬에 하나만 있는 전제. CommonProgress.HideAcrossScenes 와 같은 방식.
        private static BackgroundView current;

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

        [SerializeField]
        [Tooltip("별 모양 아틀라스(가로 한 줄). 비워 두면 모든 별이 절차적 원형으로 나옴.")]
        private Texture starShapes;

        [SerializeField]
        [Tooltip("플레이어 이동이 없는 씬(로비)에서 배경만 흐르게 함. 월드 유닛/초. (0, 0) 이면 끔.\n" +
                 "겹마다 시차가 다르므로 천천히 비행하는 것처럼 보임.")]
        private Vector2 idleDrift = Vector2.zero;

        [SerializeField]
        [Tooltip("켜면 가려진 상태로 시작하고 Reveal() 을 받아야 나타남.\n" +
                 "인게임 진입 전 Common Progress 동안 배경을 안 보이게 하는 용도.")]
        private bool startHidden;

        [SerializeField]
        [Tooltip("가려진 상태에서 다 나타나기까지 걸리는 시간(초).")]
        [Range(0.05F, 2.0F)]
        private float revealDuration = 0.45F;

        [SerializeField]
        [Tooltip("카메라가 한 프레임에 이보다 많이 움직이면 순간이동으로 보고 다시 가렸다 나타냄(월드 유닛).\n" +
                 "스폰 직후와 리스폰의 카메라 점프를 가림. 0 이면 끔.")]
        private float teleportThreshold = 40.0F;

        [SerializeField]
        [Tooltip("startHidden 으로 가린 뒤 이 시간이 지나도 Reveal() 이 안 오면 그냥 나타남(초).\n" +
                 "정상 경로는 FollowPlayerEntity 가 카메라를 옮긴 뒤 Reveal() 을 부르는 것임.\n" +
                 "이 값은 그 호출이 통째로 누락됐을 때만 쓰이는 안전장치라,\n" +
                 "접속과 스폰에 걸리는 시간보다 넉넉히 길어야 함. 짧으면 원점 배경이 먼저 보임. 0 이면 끔.")]
        private float revealTimeout = 30.0F;

        // 마지막으로 맞춘 화면 크기. 같으면 Transform 을 안 건드림.
        private Vector2 appliedSize;

        // 마지막으로 켠 겹 수. 0 이면 아직 안 켬.
        private int appliedOctaves;

        // 로비용 가상 카메라 이동 누적값.
        private Vector2 driftOffset;

        // 1 = 완전히 가려짐, 0 = 다 보임.
        private float fadeOut;

        // Reveal() 을 받았는가. startHidden 이 꺼져 있으면 처음부터 true.
        private bool revealed;

        // startHidden 으로 가린 뒤 흐른 시간. revealTimeout 안전장치용.
        private float hiddenElapsed;

        // 순간이동 판정용 직전 카메라 위치.
        private Vector2 previousCamera;
        private bool hasPreviousCamera;

        /// <summary>
        /// 인게임 진입이 끝났음을 알림. startHidden 으로 가려 둔 배경이 이때부터 나타남.
        /// 씬에 BackgroundView 가 없으면 아무 일도 안 함.
        /// </summary>
        public static void RevealAcrossScenes()
        {
            if (current != null)
            {
                current.revealed = true;
            }
        }

        private void Awake()
        {
            if (targetCamera == null)
            {
                targetCamera = UnityEngine.Camera.main;
            }
        }

        private void OnEnable()
        {
            current = this;

            appliedSize = Vector2.zero;
            driftOffset = Vector2.zero;
            hasPreviousCamera = false;

            revealed = !startHidden;
            fadeOut = startHidden ? 1.0F : 0.0F;
            hiddenElapsed = 0.0F;

            QualitySettings.activeQualityLevelChanged += OnQualityChanged;
            ApplyStarOctaves();
            ApplyStarShapes();
            ApplyGlobals();
        }

        private void OnDisable()
        {
            QualitySettings.activeQualityLevelChanged -= OnQualityChanged;

            if (current == this)
            {
                current = null;
            }

            // 다음 씬이 전역을 물려받지 않게 중립으로 되돌림. 둘 다 0 이 정상 동작임.
            Shader.SetGlobalVector(CameraOffsetId, Vector4.zero);
            Shader.SetGlobalFloat(FadeOutId, 0.0F);
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

            UpdateDriftAndFade(new Vector2(cameraPosition.x, cameraPosition.y));

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
        /// 로비용 가상 이동과 가림 상태를 갱신함.
        /// 카메라가 한 프레임에 크게 튀면(스폰 / 리스폰) 다시 가렸다가 나타나게 해서
        /// 배경이 "이동 거리 x 시차" 만큼 순간이동하는 것을 안 보이게 함.
        /// </summary>
        private void UpdateDriftAndFade(Vector2 cameraXY)
        {
            if (idleDrift != Vector2.zero)
            {
                driftOffset += idleDrift * Time.deltaTime;
            }

            if (hasPreviousCamera
                && teleportThreshold > 0.0F
                && (cameraXY - previousCamera).sqrMagnitude > teleportThreshold * teleportThreshold)
            {
                fadeOut = 1.0F;
            }

            previousCamera = cameraXY;
            hasPreviousCamera = true;

            // Reveal 이 누락된 경로가 있어도 배경이 영영 안 나오는 일은 없게 함.
            if (!revealed && revealTimeout > 0.0F)
            {
                hiddenElapsed += Time.deltaTime;

                if (hiddenElapsed >= revealTimeout)
                {
                    revealed = true;
                }
            }

            if (revealed && fadeOut > 0.0F)
            {
                fadeOut = Mathf.Max(0.0F, fadeOut - Time.deltaTime / Mathf.Max(revealDuration, 1e-3F));
            }

            ApplyGlobals();
        }

        private void ApplyGlobals()
        {
            Shader.SetGlobalVector(CameraOffsetId, new Vector4(driftOffset.x, driftOffset.y, 0.0F, 0.0F));
            Shader.SetGlobalFloat(FadeOutId, fadeOut);
        }

        /// <summary>
        /// 별 모양 아틀라스를 전역 텍스처로 넣음. 머티리얼 프로퍼티가 아니라 전역이라
        /// 그래프와 머티리얼을 건드리지 않고 추가할 수 있음(겹 수 키워드와 같은 방식).
        /// 비어 있으면 셰이더가 검정을 읽어 텍스처 모양 별만 사라지고 원형 별은 그대로 나옴.
        /// </summary>
        private void ApplyStarShapes()
        {
            if (starShapes != null)
            {
                Shader.SetGlobalTexture(StarShapesId, starShapes);
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
