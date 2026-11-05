using GalaxyIO.DOTS.Components.Actor;
using GalaxyIO.Tools;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.NetCode;
using UnityEngine;
using Random = UnityEngine.Random;

namespace GalaxyIO.Scripts.Managed.Effects
{
    /// <summary>
    /// 속도감을 주는 우주 먼지 줄무늬. 클라이언트 전용.
    /// 카메라 주변에 깊이(z)가 다른 먼지 점을 깔고, 내 우주선 속도 반대쪽으로 꼬리를 그림.
    /// 빠를수록 꼬리가 길고 진해지며, 가까운 먼지(z 큼)일수록 빨리 지나감(시차).
    /// 먼지는 화면을 벗어나면 반대편으로 다시 들어오므로 개수가 일정함.
    /// </summary>
    [RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
    public sealed class SpeedDustView : MonoBehaviour
    {
        [SerializeField]
        [Tooltip("가산 이펙트 머티리얼(GalaxyIO/FX Additive)")]
        private Material material;

        [SerializeField]
        [Tooltip("먼지 수")]
        [Min(0)]
        private int count = 70;

        [SerializeField]
        [Tooltip("먼지 색(HDR 아님, 은은하게)")]
        private Color color = new Color(0.62F, 0.77F, 1.0F, 1.0F);

        [SerializeField]
        [Tooltip("꼬리 길이 = 속력 x 이 값 x 깊이 (월드 단위)")]
        [Min(0.0F)]
        private float lengthPerSpeed = 0.06F;

        [SerializeField]
        [Tooltip("꼬리 길이 범위(월드 단위)")]
        private Vector2 lengthRange = new Vector2(0.05F, 6.0F);

        [SerializeField]
        [Tooltip("알파: 기본 + 속력 / 이 값 (최대 maxAlpha)")]
        [Min(0.01F)]
        private float alphaPerSpeed = 120.0F;

        [SerializeField]
        [Range(0.0F, 1.0F)]
        private float baseAlpha = 0.12F;

        [SerializeField]
        [Range(0.0F, 1.0F)]
        private float maxAlpha = 0.6F;

        [SerializeField]
        [Tooltip("이 속력 아래면 먼지를 그리지 않음(멈춰 있을 때 화면을 어지럽히지 않게)")]
        [Min(0.0F)]
        private float minSpeed = 2.0F;

        private Vector3[] dust;
        private FxQuadBatch batch;
        private World clientWorld;
        private EntityQuery shipQuery;
        private UnityEngine.Camera targetCamera;
        private Vector2 lastCamera;
        private bool hasLastCamera;

        private void Awake()
        {
            // 서버에서 비활성화함
            if (Application.isBatchMode)
            {
                gameObject.SetActive(false);
            }
        }

        private void Start()
        {
            clientWorld = ClientServerBootstrap.ClientWorld;
            targetCamera = UnityEngine.Camera.main;

            if (clientWorld == null || targetCamera == null)
            {
                enabled = false;
                return;
            }

            batch = new FxQuadBatch("SpeedDust");
            GetComponent<MeshFilter>().sharedMesh = batch.Mesh;

            material = material != null ? new Material(material) : new Material(Shader.Find("GalaxyIO/FX Additive"));
            material.mainTexture = FxAtlas.Texture;
            GetComponent<MeshRenderer>().sharedMaterial = material;

            shipQuery = clientWorld.EntityManager.CreateEntityQuery(
                ComponentType.ReadOnly<GhostOwnerIsLocal>(),
                ComponentType.ReadOnly<MovementComponent>(),
                ComponentType.ReadOnly<PlayerSpaceshipMovementComponent>()
            );

            // 화면 크기 기준 0 ~ 1 좌표와 깊이(0.3 ~ 1)
            dust = new Vector3[count];

            for (int i = 0; i < count; i++)
            {
                dust[i] = new Vector3(Random.value, Random.value, Random.Range(0.3F, 1.0F));
            }
        }

        private void OnDestroy()
        {
            if (material != null)
            {
                Destroy(material);
            }
        }

        private void LateUpdate()
        {
            // 세션을 나가면 클라이언트 월드가 Dispose 됨. 캐시한 쿼리를 더 쓰면 안 됨.
            if (!ClientWorldScope.IsAlive(clientWorld))
            {
                batch.Begin();
                batch.End();
                enabled = false;
                return;
            }

            Vector2 velocity = Vector2.zero;

            if (!shipQuery.IsEmpty)
            {
                using NativeArray<MovementComponent> movements = shipQuery.ToComponentDataArray<MovementComponent>(Allocator.Temp);
                float2 raw = movements[0].Velocity;
                velocity = new Vector2(raw.x, raw.y);
            }

            float height = targetCamera.orthographicSize * 2.0F;
            Vector2 view = new Vector2(height * targetCamera.aspect, height) * 1.1F;
            Vector2 camera = targetCamera.transform.position;

            // 카메라가 움직인 만큼 깊이 비율로 거꾸로 밀어 시차를 만듦. 크게 순간이동하면 다시 깖.
            Vector2 moved = hasLastCamera ? camera - lastCamera : Vector2.zero;
            hasLastCamera = true;
            lastCamera = camera;

            if (moved.magnitude > view.x)
            {
                moved = Vector2.zero;
            }

            batch.Begin();
            float speed = velocity.magnitude;

            for (int i = 0; i < dust.Length; i++)
            {
                Vector3 point = dust[i];

                // 깊이만큼 덜 따라옴(가까울수록 빨리 지나감).
                point.x = Mathf.Repeat(point.x - moved.x * (1.0F - point.z * 0.5F) / view.x, 1.0F);
                point.y = Mathf.Repeat(point.y - moved.y * (1.0F - point.z * 0.5F) / view.y, 1.0F);
                dust[i] = point;

                if (speed < minSpeed)
                {
                    continue;
                }

                Vector2 world = camera + new Vector2((point.x - 0.5F) * view.x, (point.y - 0.5F) * view.y);
                float length = Mathf.Clamp(speed * lengthPerSpeed * point.z, lengthRange.x, lengthRange.y);
                float alpha = Mathf.Min(baseAlpha + speed / alphaPerSpeed, maxAlpha) * point.z;

                // 한 픽셀 굵기(가까우면 조금 굵게). 밝은 머리가 먼지 자리, 꼬리는 진행 반대쪽으로 뻗음.
                float pixel = height / Mathf.Max(Screen.height, 1);
                float width = pixel * (1.0F + point.z);
                Color tint = color;
                tint.a = alpha;
                batch.Add(world, new Vector2(width, length), velocity, FxAtlas.Streak, tint, 1.0F);
            }

            batch.End();
        }
    }
}
