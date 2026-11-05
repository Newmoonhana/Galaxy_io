using System.Collections.Generic;
using GalaxyIO.DOTS.Client.Components.Message;
using GalaxyIO.DOTS.Components.Singleton;
using GalaxyIO.Camera;
using GalaxyIO.Tools;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.NetCode;
using Unity.Transforms;
using UnityEngine;
using Random = UnityEngine.Random;

namespace GalaxyIO.Scripts.Managed.Effects
{
    /// <summary>
    /// 우주선 크기에 따라 파괴 연출을 키우는 클라이언트 전용 뷰.
    /// 기본 폭발(<see cref="PrefabRegistryComponent.ExplosionEffect"/>)과 폭발음은 파괴 수신 시스템이 그대로 냄.
    /// 이 뷰는 그 위에 덧붙임.
    /// <list type="bullet">
    /// <item>충격 고리, 파편, 연기: 선체 반지름에 비례.</item>
    /// <item>큰 배(반지름 bigRadius 이상): 선체 곳곳에서 연달아 터지는 추가 폭발, 큰 폭발음, 화면 안이면 흔들림.</item>
    /// <item>내 우주선이 터지면 강한 흔들림. 내가 격추하면 약한 흔들림.</item>
    /// </list>
    /// </summary>
    [RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
    public sealed class DestructionFxView : MonoBehaviour
    {
        [SerializeField]
        [Tooltip("가산 이펙트 머티리얼(GalaxyIO/FX Additive)")]
        private Material material;

        [SerializeField]
        [Tooltip("이 반지름(월드 단위) 이상이면 큰 배로 보고 연쇄 폭발을 냄")]
        [Min(0.0F)]
        private float bigRadius = 4.0F;

        [SerializeField]
        [Tooltip("큰 배 추가 폭발 수 = 반지름 / 이 값 (1 ~ 10)")]
        [Min(0.1F)]
        private float radiusPerExplosion = 2.0F;

        [SerializeField]
        [Tooltip("추가 폭발 사이 간격(초)")]
        [Min(0.0F)]
        private float chainInterval = 0.09F;

        [SerializeField]
        [Tooltip("큰 배가 터질 때 한 번 더 내는 폭발음(PF_Core 사운드 그룹)")]
        private string bigExplosionSound = "ThorExplosion";

        [SerializeField]
        [Tooltip("충격 고리 색(HDR)")]
        [ColorUsage(true, true)]
        private Color ringColor = new Color(2.0F, 1.5F, 0.8F, 1.0F);

        [SerializeField]
        [Tooltip("파편 색(HDR). 무작위로 섞음.")]
        [ColorUsage(true, true)]
        private Color[] debrisColors =
        {
            new Color(2.0F, 1.2F, 0.4F, 1.0F),
            new Color(2.0F, 0.6F, 0.3F, 1.0F),
            new Color(0.9F, 0.9F, 0.95F, 1.0F),
        };

        [SerializeField]
        [Tooltip("연기 색")]
        private Color smokeColor = new Color(0.35F, 0.32F, 0.4F, 0.6F);

        [SerializeField]
        [Tooltip("흔들림 세기(FollowPlayerEntity.Shake 에 넘기는 값): 내 우주선 파괴 / 화면 안 큰 배 / 내가 격추(큰 배, 작은 배)")]
        private Vector4 shake = new Vector4(0.24F, 0.1F, 0.12F, 0.03F);

        private enum Kind
        {
            Ring,
            Debris,
            Smoke,
        }

        private struct Particle
        {
            public Kind Kind;
            public Vector2 Position;
            public Vector2 Velocity;
            public float Age;
            public float Life;
            public float Size;
            public Color Color;
            public float Delay;
        }

        private struct Pending
        {
            public float Time;
            public float2 Position;
        }

        private readonly List<Particle> particles = new List<Particle>(512);
        private readonly List<Pending> chain = new List<Pending>();

        private FxQuadBatch batch;
        private World clientWorld;
        private EntityQuery messageQuery;
        private EntityQuery registryQuery;
        private FollowPlayerEntity follow;
        private UnityEngine.Camera mainCamera;

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

            if (clientWorld == null)
            {
                enabled = false;
                return;
            }

            batch = new FxQuadBatch("DestructionFx");
            GetComponent<MeshFilter>().sharedMesh = batch.Mesh;

            material = material != null ? new Material(material) : new Material(Shader.Find("GalaxyIO/FX Additive"));
            material.mainTexture = FxAtlas.Texture;
            GetComponent<MeshRenderer>().sharedMaterial = material;

            messageQuery = clientWorld.EntityManager.CreateEntityQuery(ComponentType.ReadOnly<SpaceshipDestroyedFxMessage>());
            registryQuery = clientWorld.EntityManager.CreateEntityQuery(ComponentType.ReadOnly<PrefabRegistryComponent>());
            follow = FindFirstObjectByType<FollowPlayerEntity>();
            mainCamera = UnityEngine.Camera.main;
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

            EntityManager entityManager = clientWorld.EntityManager;

            if (!messageQuery.IsEmpty)
            {
                using NativeArray<SpaceshipDestroyedFxMessage> messages = messageQuery.ToComponentDataArray<SpaceshipDestroyedFxMessage>(Allocator.Temp);

                foreach (SpaceshipDestroyedFxMessage message in messages)
                {
                    Play(entityManager, message);
                }

                entityManager.DestroyEntity(messageQuery);
            }

            SpawnChain(entityManager);

            batch.Begin();
            UpdateParticles(Time.deltaTime);
            batch.End();
        }

        private void Play(EntityManager entityManager, in SpaceshipDestroyedFxMessage message)
        {
            float radius = math.max(message.Radius, 1.0F);
            bool big = message.Radius >= bigRadius;
            Vector2 center = new Vector2(message.Position.x, message.Position.y);
            Vector2 inherit = new Vector2(message.Velocity.x, message.Velocity.y);

            // 충격 고리
            particles.Add(new Particle
            {
                Kind = Kind.Ring,
                Position = center,
                Life = big ? 0.7F : 0.4F,
                Size = radius * (big ? 4.0F : 3.2F),
                Color = ringColor,
            });

            // 파편: 터진 배 속도의 일부를 물려받아 흩어짐.
            int debris = big ? 26 : 8;

            for (int i = 0; i < debris; i++)
            {
                Vector2 direction = Random.insideUnitCircle.normalized;
                particles.Add(new Particle
                {
                    Kind = Kind.Debris,
                    Position = center,
                    Velocity = inherit * 0.4F + direction * Random.Range(radius * 2.0F, radius * (big ? 9.0F : 6.0F)),
                    Life = Random.Range(0.4F, 1.1F),
                    Size = Random.Range(0.15F, big ? 0.45F : 0.3F) * Mathf.Sqrt(radius),
                    Color = debrisColors.Length > 0 ? debrisColors[Random.Range(0, debrisColors.Length)] : Color.white,
                });
            }

            // 연기
            int smoke = big ? 10 : 3;

            for (int i = 0; i < smoke; i++)
            {
                particles.Add(new Particle
                {
                    Kind = Kind.Smoke,
                    Position = center + Random.insideUnitCircle * radius * 0.5F,
                    Velocity = inherit * 0.2F + Random.insideUnitCircle * radius * 0.6F,
                    Life = Random.Range(0.8F, 1.6F),
                    Size = radius * Random.Range(0.8F, 1.6F) * (big ? 1.5F : 1.0F),
                    Color = smokeColor,
                    Delay = Random.Range(0.0F, 0.15F),
                });
            }

            // 큰 배: 선체 곳곳에서 연달아 추가 폭발 + 큰 폭발음
            if (big)
            {
                int count = Mathf.Clamp(Mathf.RoundToInt(message.Radius / radiusPerExplosion), 1, 10);

                for (int i = 0; i < count; i++)
                {
                    Vector2 offset = Random.insideUnitCircle * message.Radius * 0.8F;
                    chain.Add(new Pending
                    {
                        Time = Time.time + (i + 1) * chainInterval,
                        Position = message.Position + new float2(offset.x, offset.y),
                    });
                }

                if (!string.IsNullOrEmpty(bigExplosionSound))
                {
                    Entity sound = entityManager.CreateEntity();
                    entityManager.AddComponentData(sound, new SoundMessage
                    {
                        SoundKey = new FixedString64Bytes(bigExplosionSound),
                        Position = message.Position,
                    });
                }
            }

            // 흔들림
            if (follow != null)
            {
                if (message.Local)
                {
                    follow.Shake(shake.x);
                }
                else if (message.KilledByLocal)
                {
                    follow.Shake(big ? shake.z : shake.w);
                }
                else if (big && OnScreen(center))
                {
                    follow.Shake(shake.y);
                }
            }
        }

        private bool OnScreen(Vector2 point)
        {
            if (mainCamera == null)
            {
                return false;
            }

            Vector3 viewport = mainCamera.WorldToViewportPoint(point);
            return viewport.x > -0.1F && viewport.x < 1.1F && viewport.y > -0.1F && viewport.y < 1.1F;
        }

        // 예약해 둔 추가 폭발을 때가 되면 원작 폭발 연출로 만듦.
        private void SpawnChain(EntityManager entityManager)
        {
            if (chain.Count == 0 || registryQuery.IsEmpty)
            {
                return;
            }

            Entity prefab = registryQuery.GetSingleton<PrefabRegistryComponent>().ExplosionEffect;

            for (int i = chain.Count - 1; i >= 0; i--)
            {
                if (chain[i].Time > Time.time)
                {
                    continue;
                }

                if (prefab != Entity.Null)
                {
                    Entity explosion = entityManager.Instantiate(prefab);
                    LocalTransform transform = entityManager.GetComponentData<LocalTransform>(prefab);
                    transform.Position = new float3(chain[i].Position, 0.0F);
                    entityManager.SetComponentData(explosion, transform);
                }

                chain.RemoveAt(i);
            }
        }

        private void UpdateParticles(float deltaTime)
        {
            for (int i = particles.Count - 1; i >= 0; i--)
            {
                Particle particle = particles[i];

                if (particle.Delay > 0.0F)
                {
                    particle.Delay -= deltaTime;
                    particles[i] = particle;
                    continue;
                }

                particle.Age += deltaTime;

                if (particle.Age >= particle.Life)
                {
                    particles[i] = particles[particles.Count - 1];
                    particles.RemoveAt(particles.Count - 1);
                    continue;
                }

                particle.Position += particle.Velocity * deltaTime;
                particle.Velocity *= 1.0F - Mathf.Clamp01(deltaTime * (particle.Kind == Kind.Smoke ? 1.5F : 2.5F));
                particles[i] = particle;

                float t = particle.Age / particle.Life;

                switch (particle.Kind)
                {
                    case Kind.Ring:
                    {
                        float size = particle.Size * (0.3F + 0.7F * Mathf.Sqrt(t));
                        batch.Add(particle.Position, new Vector2(size, size), Vector2.up, FxAtlas.Ring, particle.Color * (1.0F - t));
                        break;
                    }
                    case Kind.Debris:
                    {
                        Vector2 heading = particle.Velocity.sqrMagnitude > 0.0001F ? particle.Velocity : Vector2.up;
                        batch.Add(particle.Position, new Vector2(particle.Size, particle.Size * 2.5F), heading, FxAtlas.Streak, particle.Color * (1.0F - t));
                        break;
                    }
                    case Kind.Smoke:
                    {
                        float size = particle.Size * (0.6F + t * 1.2F);
                        Color color = particle.Color;
                        color.a *= 1.0F - t;
                        batch.Add(particle.Position, new Vector2(size, size), Vector2.up, FxAtlas.Glow, color * 0.35F);
                        break;
                    }
                }
            }
        }
    }
}
